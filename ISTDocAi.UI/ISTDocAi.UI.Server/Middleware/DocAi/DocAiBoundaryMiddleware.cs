using ISTDocAi.UI.Server.Services.DocAi;
using ISTDocAi.UI.Server.Base.DocAi;
using System.Globalization;
using ISTDocAi.Application.Common.Interfaces.DocAi;
namespace ISTDocAi.UI.Server.Middleware.DocAi;

public sealed class DocAiBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDatabaseReadiness readiness, ILogger<DocAiBoundaryMiddleware> logger)
    {
        if (context.Request.Path.StartsWithSegments("/api/v2/usage/topups"))
            context.Response.Headers.CacheControl = "no-store";
        var supplied = context.Request.Headers["X-Correlation-Id"].ToString();
        var correlation = supplied.Length is > 0 and <= 64 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            ? supplied : Guid.NewGuid().ToString("N");
        context.Items["correlationId"] = correlation;
        context.Response.OnStarting(() => { context.Response.Headers["X-Correlation-Id"] = correlation; return Task.CompletedTask; });
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlation });
        if (context.Request.Path == "/health/live") { context.Response.StatusCode = 200; return; }
        if (context.Request.Path == "/health/ready")
        {
            try
            {
                if (readiness.Status.State != DatabaseReadinessState.Ready) { context.Response.StatusCode = 503; return; }
                context.Response.StatusCode = await ISTDocAi.Infrastructure.Services.DocAi.Analysis.DocAiReadiness.CheckDependenciesAsync(context.RequestServices, context.RequestAborted) ? 200 : 503;
            }
            catch (Exception) when (!context.RequestAborted.IsCancellationRequested) { context.Response.StatusCode = 503; }
            return;
        }
        if (context.Request.Path.StartsWithSegments("/api/v2"))
        {
            var status = readiness.Status;
            if (status.State != DatabaseReadinessState.Ready)
            {
                var previous = CultureInfo.CurrentUICulture;
                try {
                    CultureInfo.CurrentUICulture = await DocAiRequestCulture.SelectAsync(context);
                    await DocAiTransport.Failure(context, new(503, status.Code, "Service unavailable", "Database initialization is not complete."), context.RequestAborted);
                } finally { CultureInfo.CurrentUICulture = previous; }
                return;
            }
        }
        await next(context);
    }
}
