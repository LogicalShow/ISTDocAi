using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.Application.Common.Localization;

namespace ISTDocAi.UI.Server.Base;

[Authorize]
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected IActionResult Respond(
        Result result,
        int successStatusCode = StatusCodes.Status200OK)
    {
        return result.IsSuccess
            ? StatusCode(successStatusCode, result)
            : HandleFailure(result);
    }

    protected IActionResult Respond<TValue>(
        Result<TValue> result,
        Func<TValue, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : HandleFailure(result);
    }

    protected IActionResult Respond()
    {
        return Respond(Result.Success());
    }

    private IActionResult HandleFailure(Result result)
    {
        var localizer = HttpContext.RequestServices.GetRequiredService<IMessageLocalizer>();
        var localizedResult = localizer.Localize(result);
        var localizedError = localizedResult.Error;

        return localizedError.Type switch
        {
            ErrorType.Validation => BadRequest(localizedResult),
            ErrorType.BusinessRule => UnprocessableEntity(localizedResult),
            ErrorType.NotFound => NotFound(localizedResult),
            ErrorType.Unauthorized => Unauthorized(localizedResult),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, localizedResult),
            ErrorType.Conflict => Conflict(localizedResult),
            ErrorType.Unexpected => StatusCode(StatusCodes.Status500InternalServerError, localizedResult),
            ErrorType.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, localizedResult),
            ErrorType.Failure => BadRequest(localizedResult),
            ErrorType.None => throw new InvalidOperationException(
                "A failed result cannot contain an error with type None."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(localizedError.Type),
                localizedError.Type,
                "Unsupported error type.")
        };
    }
    protected string? GetUserIdFromToken()
    {
        return User?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
    }
}
