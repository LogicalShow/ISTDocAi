using System.Globalization;
using System.Threading.RateLimiting;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ISTDocAi.UI.Server.RateLimiting;

public static class RateLimitingExtensions
{
    /// <summary>Header the client reads to decide how long to wait before retrying.</summary>
    private const string RetryAfterHeader = "Retry-After";

    public static IServiceCollection AddAppRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                      ?? new RateLimitingOptions();

        if (!options.Enabled) return services;

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Sliding window per caller. Static files and the SPA fallback are served
            // before this middleware runs, but the API and the deep-link fallback both
            // pass through here - only /api is metered so a page refresh never spends
            // a caller's budget.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/v2") || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetNoLimiter("static");

                return RateLimitPartition.GetSlidingWindowLimiter(
                    GetPartitionKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = options.Global.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Global.WindowSeconds),
                        SegmentsPerWindow = options.Global.SegmentsPerWindow,
                        QueueLimit = options.Global.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // The dashboard opens with one request per widget - today that is seven and
            // it grows with every widget added. A concurrency limiter with a deep queue
            // spreads that burst over the database without failing any of it: requests
            // past the permit limit wait their turn instead of coming back 429.
            limiter.AddPolicy(RateLimitPolicies.Dashboard, context =>
                RateLimitPartition.GetConcurrencyLimiter(
                    GetPartitionKey(context),
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = options.Dashboard.PermitLimit,
                        QueueLimit = options.Dashboard.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Auth endpoints are unauthenticated, so the partition is the client address.
            limiter.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.Auth.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Auth.WindowSeconds),
                        QueueLimit = options.Auth.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = GetRetryAfterSeconds(context.Lease, options);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                context.HttpContext.Response.Headers[RetryAfterHeader] =
                    retryAfter.ToString(CultureInfo.InvariantCulture);

                var localizer = context.HttpContext.RequestServices.GetRequiredService<IMessageLocalizer>();
                var body = Result.Failure(Error.RateLimited(
                    ErrorCodes.RateLimited,
                    localizer.Localize(
                        ErrorCodes.RateLimited,
                        "Too many requests. Please try again shortly.")));

                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");

                logger.LogWarning(
                    "Rate limit rejected {Method} {Path} for {Partition}; retry after {RetryAfter}s",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    GetPartitionKey(context.HttpContext),
                    retryAfter);

                await context.HttpContext.Response.WriteAsJsonAsync(
                    body,
                    cancellationToken);
            };
        });

        return services;
    }

    public static IApplicationBuilder UseAppRateLimiting(this WebApplication app)
    {
        var enabled = app.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Enabled;

        return enabled ? app.UseRateLimiter() : app;
    }

    /// <summary>
    /// One bucket per signed-in user so a busy user can never exhaust another user's
    /// budget; anonymous callers fall back to their address.
    /// </summary>
    private static string GetPartitionKey(HttpContext context)
    {
        var userId = context.User?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        return string.IsNullOrWhiteSpace(userId) ? GetClientAddress(context) : $"user:{userId}";
    }

    private static string GetClientAddress(HttpContext context) =>
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static int GetRetryAfterSeconds(RateLimitLease lease, RateLimitingOptions options)
    {
        // A window limiter reports when the window rolls over; the concurrency limiter
        // has no such metadata, so fall back to a short wait the client can back off on.
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) && retryAfter.TotalSeconds >= 1)
            return (int)Math.Ceiling(retryAfter.TotalSeconds);

        return Math.Max(1, Math.Min(options.Global.WindowSeconds, 5));
    }
}
