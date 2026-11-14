using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Mvc;

namespace ISTDocAi.UI.Server.Authorization;

public static class ApiAuthorizationFailureFactory
{
    public static Result CreateUnauthorized(HttpContext context) =>
        Create(
            context,
            Error.Unauthorized(
                ErrorCodes.AuthUnauthorized,
                "Authentication is required."));

    public static Result CreateForbidden(HttpContext context) =>
        Create(
            context,
            Error.Forbidden(
                ErrorCodes.AuthForbidden,
                "You do not have permission to perform this action."));

    public static IActionResult CreateUnauthorizedActionResult(
        HttpContext context) =>
        CreateActionResult(
            CreateUnauthorized(context),
            StatusCodes.Status401Unauthorized);

    public static IActionResult CreateForbiddenActionResult(
        HttpContext context) =>
        CreateActionResult(
            CreateForbidden(context),
            StatusCodes.Status403Forbidden);

    private static Result Create(HttpContext context, Error error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        var localizer = context.RequestServices
            .GetRequiredService<IMessageLocalizer>();

        return localizer.Localize(Result.Failure(error));
    }

    private static IActionResult CreateActionResult(
        Result result,
        int statusCode) =>
        new ObjectResult(result)
        {
            StatusCode = statusCode
        };
}
