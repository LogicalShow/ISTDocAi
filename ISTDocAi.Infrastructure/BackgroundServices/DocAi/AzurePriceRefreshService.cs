using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;
public sealed class AzurePriceRefreshService(IServiceScopeFactory scopes, IOptions<AzurePriceRefreshOptions> options,
    TimeProvider clock, IDatabaseReadiness readiness, ILogger<AzurePriceRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        try
        {
            while (true)
            {
                try { await readiness.WaitUntilReadyAsync(stoppingToken); break; }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    logger.LogError("{event}", "azure_price_refresh.readiness_failed");
                    await Task.Delay(options.Value.Interval, clock, stoppingToken);
                }
            }
            await Task.Delay(options.Value.StartupDelay, clock, stoppingToken);
            var skip = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var latest = await scope.ServiceProvider.GetRequiredService<IAzurePriceCheckStore>().LatestCompletedAsync(stoppingToken);
                skip = latest is not null && latest > clock.GetUtcNow() - options.Value.Interval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("{event}", "azure_price_refresh.history_failed"); }
            using var timer = new PeriodicTimer(options.Value.Interval, clock);
            if (!skip) await RunAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CheckAzurePrices>().RunAsync(ct);
            logger.LogInformation("{event} outcome={outcome}", "azure_price_refresh.completed", string.Join(",", result.Select(x => x.Outcome)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { logger.LogError("{event}", "azure_price_refresh.failed"); }
    }
}
