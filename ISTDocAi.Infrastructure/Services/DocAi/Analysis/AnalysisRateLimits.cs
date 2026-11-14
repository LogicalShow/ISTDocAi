using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Analysis;

public sealed class AnalysisRateLimits : IDisposable
{
    private readonly TokenBucketRateLimiter analyze;
    private readonly TokenBucketRateLimiter poll;
    private readonly TimeSpan wait;
    public AnalysisRateLimits(IOptions<DocumentIntelligenceOptions> options)
    {
        var o = options.Value;
        if (o.AnalyzePerSecond < 1 || o.PollPerSecond < 1 || o.QueueLimit < 1 || o.TokenWaitTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Positive rate limits and bounded token wait are required.");
        analyze = Create(o.AnalyzePerSecond, o.QueueLimit);
        poll = Create(o.PollPerSecond, o.QueueLimit);
        wait = o.TokenWaitTimeout;
    }
    private static TokenBucketRateLimiter Create(int rate, int queue) => new(new()
    {
        TokenLimit = rate, TokensPerPeriod = rate, ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        AutoReplenishment = true, QueueLimit = queue, QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });
    public async Task WaitAsync(bool isPost, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(wait);
        try
        {
            using var lease = await (isPost ? analyze : poll).AcquireAsync(1, budget.Token);
            if (!lease.IsAcquired) throw new AnalysisUnavailableException(AnalysisUnavailableReason.RateLimited);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AnalysisUnavailableException(AnalysisUnavailableReason.RateLimited);
        }
    }
    public void Dispose() { analyze.Dispose(); poll.Dispose(); }
}
