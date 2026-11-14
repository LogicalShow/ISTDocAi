using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;

public sealed class RetentionService(IServiceScopeFactory scopes, IOptions<RetentionOptions> options,
    TimeProvider clock, ILogger<RetentionService> logger, IDatabaseReadiness readiness) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await readiness.WaitUntilReadyAsync(stoppingToken);
        DateTimeOffset? lastRun = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = clock.GetUtcNow().ToUniversalTime();
                var next = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero) + options.Value.RunAtUtc;
                if (next < now || lastRun >= next) next = next.AddDays(1);
                await Task.Delay(next - now, clock, stoppingToken);
                stoppingToken.ThrowIfCancellationRequested();
                lastRun = next;
                var started = clock.GetTimestamp();
                logger.LogInformation("{event}", "retention.started");
                await using (var scope = scopes.CreateAsyncScope())
                    await scope.ServiceProvider.GetRequiredService<IRetention>().RunAsync(stoppingToken);
                logger.LogInformation("{event} durationMs={durationMs}", "retention.completed", clock.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (SqlException e) when (e.Number == 1205) { logger.LogWarning("{event}", "retention.failed"); }
            catch (Exception) { logger.LogError("{event}", "retention.failed"); }
        }
    }
}
