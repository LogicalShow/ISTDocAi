using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.Extensions.Localization;

namespace ISTDocAi.Application.Common.Localization;

public sealed class MessageLocalizer(IStringLocalizer<Messages> localizer) : IMessageLocalizer
{
    public string Localize(string key, string fallback, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return FormatFallback(fallback, args);
        }

        var localized = args.Length == 0 ? localizer[key] : localizer[key, args];

        return localized.ResourceNotFound
            ? FormatFallback(fallback, args)
            : localized.Value;
    }

    public Error Localize(Error error)
    {
        if (error is AggregateError aggregateError)
        {
            return new AggregateError(aggregateError.Errors.Select(Localize));
        }

        return error.WithMessage(Localize(error.Code, error.Message));
    }

    public Result Localize(Result result)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        if (result is IValidationResult validationResult)
        {
            return ValidationResult.WithErrors(validationResult.Errors.Select(Localize).ToArray());
        }

        if (result.Error is AggregateError aggregateError)
        {
            return Result.Failure(aggregateError.Errors.Select(Localize).ToArray());
        }

        return Result.Failure(Localize(result.Error));
    }

    private static string FormatFallback(string fallback, object[] args)
    {
        if (args.Length == 0)
        {
            return fallback;
        }

        try
        {
            return string.Format(fallback, args);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
