using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ISTDocAi.UI.Server.Validation;

public static class ApiValidationResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var localizer = context.HttpContext.RequestServices
            .GetRequiredService<IMessageLocalizer>();

        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(modelError =>
                Error.Validation(
                    ResolveCode(entry.Key),
                    ResolveMessage(modelError, localizer))))
            .ToArray();

        var result = ValidationResult.WithErrors(errors);

        return new BadRequestObjectResult(localizer.Localize(result));
    }

    private static string ResolveCode(string modelStateKey) =>
        string.IsNullOrWhiteSpace(modelStateKey)
            ? ErrorCodes.ValidationError
            : modelStateKey;

    private static string ResolveMessage(
        ModelError modelError,
        IMessageLocalizer localizer) =>
        string.IsNullOrWhiteSpace(modelError.ErrorMessage)
            ? localizer.Localize(
                ErrorCodes.ValidationError,
                "The request is invalid.")
            : modelError.ErrorMessage;
}
