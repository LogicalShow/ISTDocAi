using System.Threading.RateLimiting;
using ISTDocAi.UI.Server.Base.DocAi;
using Microsoft.Extensions.Options;
namespace ISTDocAi.UI.Server.RateLimiting.DocAi;
public sealed class DocAiConsumerRateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DocAiConsumerLimiter limiter, IOptions<DocAiRateOptions> options)
    {
        // Original MapGroup rate policy applied only to matched endpoints, not unknown routes.
        if (context.Request.Path.StartsWithSegments("/api/v2") && context.GetEndpoint() is not null)
        {
            using var lease = await limiter.Acquire(context);
            if (!lease.IsAcquired)
            {
                var seconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? (int)Math.Ceiling(retry.TotalSeconds) : options.Value.PeriodSeconds;
                await DocAiTransport.Failure(context, new(429, "RATE_LIMITED", "Request rate limit reached", "Wait before retrying.", Math.Max(1, seconds)), context.RequestAborted);
                return;
            }
        }
        await next(context);
    }
}
