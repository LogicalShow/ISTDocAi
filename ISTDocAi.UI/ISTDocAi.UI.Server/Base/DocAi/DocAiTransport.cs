using System.Globalization;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Mvc;
namespace ISTDocAi.UI.Server.Base.DocAi;

// Metadata belongs to the transport, not to an Error subclass that localization can erase.
public sealed record DocAiFailureExtensions(string CorrelationId, long? RequiredPages, long? AvailablePages, int? RetryAfter);
public sealed record DocAiFailureEnvelope(bool IsSuccess, bool IsFailure, Error Error,
    bool HasValidationErrors, IEnumerable<Error> ValidationErrors, DocAiFailureExtensions Extensions);
public static class DocAiTransport
{
    public static string Correlation(HttpContext context) => (string)context.Items["correlationId"]!;
    public static IActionResult Success<T>(HttpContext context, Result<T> result, int status = 200,
        string? location = null, int? retryAfter = null)
    {
        if (!result.IsSuccess) throw new InvalidOperationException("DocAi failures require an explicit transport status.");
        if (location is not null) context.Response.Headers.Location = location;
        if (retryAfter is int retry) context.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
        return new ObjectResult(result) { StatusCode = status };
    }
    public static async Task Failure(HttpContext context, IntakeException exception, CancellationToken ct)
    {
        var type = exception.Status switch {
            401 => ErrorType.Unauthorized, 403 => ErrorType.Forbidden, 404 => ErrorType.NotFound,
            409 => ErrorType.Conflict, 429 => ErrorType.RateLimited, 500 or 503 => ErrorType.Unexpected,
            402 or 422 => ErrorType.BusinessRule, _ => ErrorType.Validation };
        Error error = type switch {
            ErrorType.Unauthorized => Error.Unauthorized(exception.Code, exception.Message),
            ErrorType.Forbidden => Error.Forbidden(exception.Code, exception.Message),
            ErrorType.NotFound => Error.NotFound(exception.Code, exception.Message),
            ErrorType.Conflict => Error.Conflict(exception.Code, exception.Message),
            ErrorType.RateLimited => Error.RateLimited(exception.Code, exception.Message),
            ErrorType.Unexpected => Error.Unexpected(exception.Code, exception.Message),
            ErrorType.BusinessRule => Error.BusinessRule(exception.Code, exception.Message),
            _ => Error.Validation(exception.Code, exception.Message) };
        var localizer = context.RequestServices.GetRequiredService<IMessageLocalizer>();
        // Per-variant English fallback retains the original DocAi detail, including page counts.
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar") error = localizer.Localize(error);
        var localized = Result.Failure(error);
        context.Response.StatusCode = exception.Status;
        if (exception.RetryAfter is int retry) context.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
        var body = new DocAiFailureEnvelope(false, true, localized.Error, localized.HasValidationErrors,
            localized.ValidationErrors, new(Correlation(context), exception.RequiredPages, exception.AvailablePages, exception.RetryAfter));
        await context.Response.WriteAsJsonAsync(body, cancellationToken: ct);
    }
    public static IntakeException Internal() => new(500, "INTERNAL_ERROR", "Internal error",
        "An unexpected error occurred. Quote the correlationId when reporting it.");
}
