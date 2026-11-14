using System.Threading.RateLimiting;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Extensions.Options;
namespace ISTDocAi.UI.Server.RateLimiting.DocAi;
public sealed class DocAiConsumerLimiter(IOptions<DocAiRateOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> limiter = PartitionedRateLimiter.Create<HttpContext, short>(context =>
        RateLimitPartition.GetTokenBucketLimiter(((Consumer)context.Items["consumer"]!).ConsumerId,
            _ => new TokenBucketRateLimiterOptions {
                TokenLimit = Math.Max(1, options.Value.TokenLimit), TokensPerPeriod = Math.Max(1, options.Value.TokensPerPeriod),
                ReplenishmentPeriod = TimeSpan.FromSeconds(Math.Max(1, options.Value.PeriodSeconds)), AutoReplenishment = true, QueueLimit = 0 }));
    public ValueTask<RateLimitLease> Acquire(HttpContext context) => limiter.AcquireAsync(context, 1, context.RequestAborted);
    public void Dispose() => limiter.Dispose();
}
