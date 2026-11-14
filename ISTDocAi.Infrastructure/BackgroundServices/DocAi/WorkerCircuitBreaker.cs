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
public sealed class WorkerCircuitBreaker(TimeProvider clock, ILogger<WorkerCircuitBreaker> logger) : IWorkerCircuitBreaker
{
    private readonly object gate = new();
    private DateTimeOffset until;
    public TimeSpan Remaining
    {
        get { lock (gate) { var remaining = until - clock.GetUtcNow(); return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero; } }
    }
    public Task<ClaimedJob?> ClaimAsync(IJobQueue queue, CancellationToken ct)
    {
        lock (gate)
        {
            ct.ThrowIfCancellationRequested();
            // Invocation and opening are serialized; an already-started SQL claim may finish afterwards.
            return until > clock.GetUtcNow() ? Task.FromResult<ClaimedJob?>(null) : queue.ClaimAsync(ct);
        }
    }
    public void Open()
    {
        lock (gate)
        {
            if (until > clock.GetUtcNow()) return;
            until = clock.GetUtcNow().AddMinutes(5);
            logger.LogCritical("{event} errorCode={errorCode}", AlertEvents.BreakerOpened, "AZURE_AUTH");
        }
    }
}
