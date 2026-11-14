using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;

public sealed class SignalsService(IServiceScopeFactory scopes, IOptions<SignalsOptions> options,
    TimeProvider clock, ILogger<SignalsService> logger, IDatabaseReadiness readiness) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => !options.Value.Enabled ? Task.CompletedTask :
        Task.WhenAll(Loop(TimeSpan.FromMinutes(1), false, stoppingToken), Loop(TimeSpan.FromHours(1), true, stoppingToken));

    private async Task Loop(TimeSpan interval, bool billing, CancellationToken stop)
    {
        await readiness.WaitUntilReadyAsync(stop);
        using var timer = new PeriodicTimer(interval, clock);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stop)) return;
                await using var scope = scopes.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<ISignalReader>();
                if (billing)
                {
                    foreach (var row in await reader.UnpricedUsageAsync(stop))
                        logger.LogWarning("{event} meterCode={meterCode} unpricedRows={unpricedRows} unpricedPages={unpricedPages}",
                            AlertEvents.UsageUnpriced, row.MeterCode, row.UnpricedRows, row.UnpricedPages);
                }
                else if (await reader.OldestDueSecondsAsync(stop) is > 300 and var age)
                    logger.LogWarning("{event} oldestDueSeconds={oldestDueSeconds}", AlertEvents.QueueStuck, age);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Never export SQL exception messages or stop either schedule on a failed probe.
                logger.LogError("{event} signal={signal} errorCode={errorCode}", "signals.read_failed",
                    billing ? "usage" : "queue", "INTERNAL_ERROR");
            }
        }
    }
}
