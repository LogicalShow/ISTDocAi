using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ISTDocAi.UI.Server.Authorization;

public sealed class ApiAuthorizationMiddlewareResultHandler
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallbackHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (context.Request.Path.StartsWithSegments("/api/v2")
            || authorizeResult.Succeeded
            || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _fallbackHandler.HandleAsync(
                next,
                context,
                policy,
                authorizeResult);
            return;
        }

        if (!authorizeResult.Challenged && !authorizeResult.Forbidden)
        {
            await _fallbackHandler.HandleAsync(
                next,
                context,
                policy,
                authorizeResult);
            return;
        }

        // Preserve the framework's authentication-scheme behavior and headers.
        await _fallbackHandler.HandleAsync(
            next,
            context,
            policy,
            authorizeResult);

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;

        var response = authorizeResult.Challenged
            ? ApiAuthorizationFailureFactory.CreateUnauthorized(context)
            : ApiAuthorizationFailureFactory.CreateForbidden(context);

        await context.Response.WriteAsJsonAsync(
            response,
            context.RequestAborted);
    }
}
