using FluentValidation;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Infrastructure.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ISTDocAi.UI.Server.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var localizer = context.RequestServices.GetRequiredService<IMessageLocalizer>();

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors
                .Select(error => Error.Validation(
                    error.PropertyName,
                    localizer.Localize(error.ErrorMessage, error.ErrorMessage)))
                .ToArray();

            var response = ValidationResult.WithErrors(errors);

            await context.Response.WriteAsJsonAsync(
                response,
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            var localizer = context.RequestServices.GetRequiredService<IMessageLocalizer>();

            while (ex.InnerException != null) ex = ex.InnerException;

            var error = Error.Unexpected(ErrorCodes.Unexpected, ex.Message);
            var statusCode = HttpStatusCode.InternalServerError;

            if (ex.GetType().Name.EndsWith("ConcurrencyException"))
            {
                error = Error.Conflict(ErrorCodes.Concurrency, "Concurrency");
                statusCode = HttpStatusCode.Conflict;
            }
            else if (SqlErrorClassifier.IsDuplicateKey(ex))
            {
                error = Error.Conflict(ErrorCodes.UniqueConstraintViolation, "UniqueConstraintViolation");
                statusCode = HttpStatusCode.Conflict;
            }
            else if (IsDateRangeOverlap(ex.Message))
            {
                error = Error.BusinessRule(ErrorCodes.DateRangeOverlap, "DateRangeOverlap");
                statusCode = HttpStatusCode.UnprocessableEntity;
            }

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var localizedError = error.WithMessage(
                localizer.Localize(error.Code, error.Message));
            var response = Result.Failure(localizedError);

            await context.Response.WriteAsJsonAsync(
                response,
                context.RequestAborted);
        }
    }

    private static bool IsDateRangeOverlap(string message) =>
        message.Contains("Date Range Overlaps", StringComparison.OrdinalIgnoreCase) ||
        message.Contains(ErrorCodes.DateRangeOverlapValidation, StringComparison.OrdinalIgnoreCase);
}
