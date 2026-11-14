using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;
public sealed class JobProcessorService(IServiceScopeFactory scopes, IOptions<WorkerOptions> options,
    IWorkerCircuitBreaker breaker, TimeProvider clock, ILogger<JobProcessorService> logger, IDatabaseReadiness readiness) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => !options.Value.Enabled ? Task.CompletedTask :
        Task.WhenAll(Enumerable.Range(0, options.Value.MaxParallelJobs).Select(_ => Slot(stoppingToken)));

    private async Task Slot(CancellationToken stop)
    {
        await readiness.WaitUntilReadyAsync(stop);
        var idleSeconds = 2;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (breaker.Remaining is var remaining && remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, clock, stop);
                    continue;
                }
                await using var scope = scopes.CreateAsyncScope();
                stop.ThrowIfCancellationRequested();
                // Resolve only after checking the breaker: no SQL claims while it is open.
                var processor = scope.ServiceProvider.GetRequiredService<ProcessJob>();
                var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
                if (breaker.Remaining > TimeSpan.Zero) continue;
                var job = await breaker.ClaimAsync(queue, stop);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(idleSeconds), clock, stop);
                    idleSeconds = Math.Min(30, idleSeconds * 2);
                    continue;
                }
                idleSeconds = 2;
                await processor.ExecuteAsync(job, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Do not let resolution, claim, processing or disposal errors terminate a slot.
                logger.LogError("{event} errorCode={errorCode}", "worker.slot_error", "INTERNAL_ERROR");
                try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stop); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            }
        }
    }
}
