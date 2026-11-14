using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;

public sealed class WebhookSenderService(IServiceScopeFactory scopes, IOptions<WebhookOptions> options,
    TimeProvider clock, ILogger<WebhookSenderService> logger, IDatabaseReadiness readiness, ConsumerWebhookEvents? events = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await readiness.WaitUntilReadyAsync(stoppingToken);
        var idleSeconds = 2;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (events is not null)
                {
                    // Drain a bounded snapshot so a burst cannot starve ordinary deliveries.
                    for (var count = 0; count < 65536 && events.TryRead(out var consumerId); count++)
                    {
                        try
                        {
                            await using var notificationScope = scopes.CreateAsyncScope();
                            using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            budget.CancelAfter(TimeSpan.FromSeconds(5));
                            await notificationScope.ServiceProvider.GetRequiredService<IConsumerWebhookRetry>()
                                .MakeDueAsync(consumerId, budget.Token);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception) { logger.LogWarning("{event}", "webhook.consumer_alive_failed"); }
                    }
                }
                // Start before S10: claim latency must not extend our view of its SQL lease.
                var claimedAt = clock.GetTimestamp();
                IReadOnlyList<ClaimedDelivery> batch;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    batch = await scope.ServiceProvider.GetRequiredService<IWebhookOutbox>().ClaimDueAsync(stoppingToken);
                }
                if (batch.Count == 0)
                {
                    if (events is null) await Task.Delay(TimeSpan.FromSeconds(idleSeconds), clock, stoppingToken);
                    else await events.WaitAsync(TimeSpan.FromSeconds(idleSeconds), stoppingToken);
                    idleSeconds = Math.Min(30, idleSeconds * 2);
                    continue;
                }
                idleSeconds = 2;
                // Fixed slots: cancellation prevents queued work from starting, but never cancels started sends/writes.
                var index = -1;
                await Task.WhenAll(Enumerable.Range(0, options.Value.MaxParallelSends).Select(_ => Slot()));
                async Task Slot()
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var next = Interlocked.Increment(ref index);
                        if (next >= batch.Count) return;
                        if (stoppingToken.IsCancellationRequested) return;
                        try
                        {
                            await using var scope = scopes.CreateAsyncScope();
                            var delivery = scope.ServiceProvider.GetRequiredService<DeliverWebhook>();
                            // Leave a 90-second reserve; unstarted rows remain leased for a later claim.
                            if (stoppingToken.IsCancellationRequested ||
                                clock.GetElapsedTime(claimedAt) > TimeSpan.FromSeconds(210)) return;
                            await delivery.ExecuteAsync(batch[next]);
                        }
                        catch (Exception) { logger.LogError("{event}", "webhook.delivery_error"); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                logger.LogError("{event}", "webhook.sender_error");
                try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            }
        }
    }
}
