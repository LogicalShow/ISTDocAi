using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ISTDocAi.UI.Server.Idempotency;

[AttributeUsage(AttributeTargets.Method)]
public sealed class EnableIdempotencyAttribute : ActionFilterAttribute
{
    private const string DefaultContentType = "application/json; charset=utf-8";

    public int CacheMinutes { get; set; } = 15;
    public string KeyHeader { get; set; } = "Idempotency-Key";
    public bool Required { get; set; } = true;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var idempotencyKey = GetIdempotencyKey(httpContext);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (Required)
            {
                context.Result = CreateMissingKeyResult(httpContext);
                return;
            }

            await next();
            return;
        }

        var idempotencyService = httpContext.RequestServices
            .GetRequiredService<IIdempotencyService>();
        var cacheKey = CreateCacheKey(httpContext, idempotencyKey);
        var cachedResult = await GetCachedResultAsync(
            idempotencyService,
            cacheKey,
            httpContext.RequestAborted);
        if (cachedResult is not null)
        {
            context.Result = cachedResult;
            return;
        }

        var executedContext = await next();

        await CacheSuccessfulResultAsync(
            httpContext,
            idempotencyService,
            cacheKey,
            executedContext.Result);
    }

    private string? GetIdempotencyKey(HttpContext context) =>
        context.Request.Headers[KeyHeader].FirstOrDefault();

    private IActionResult CreateMissingKeyResult(HttpContext context)
    {
        var localizer = context.RequestServices
            .GetRequiredService<IMessageLocalizer>();
        var result = ValidationResult.WithErrors(
        [
            Error.Validation(
                ErrorCodes.IdempotencyKeyRequired,
                $"{KeyHeader} header is required.")
        ]);

        return new BadRequestObjectResult(localizer.Localize(result));
    }

    private static async Task<ContentResult?> GetCachedResultAsync(
        IIdempotencyService idempotencyService,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cachedValue = await idempotencyService.GetAsync(
            cacheKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(cachedValue))
        {
            return null;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<IdempotencyCacheEntry>(
                cachedValue);

            return cached is null
                ? null
                : new ContentResult
                {
                    StatusCode = cached.StatusCode,
                    ContentType = cached.ContentType,
                    Content = cached.Body
                };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task CacheSuccessfulResultAsync(
        HttpContext context,
        IIdempotencyService idempotencyService,
        string cacheKey,
        IActionResult? actionResult)
    {
        if (actionResult is not ObjectResult objectResult)
        {
            return;
        }

        if (objectResult.Value is not Result response)
        {
            return;
        }

        if (!response.IsSuccess)
        {
            return;
        }

        var statusCode = objectResult.StatusCode
            ?? StatusCodes.Status200OK;
        if (statusCode is < StatusCodes.Status200OK
            or >= StatusCodes.Status300MultipleChoices)
        {
            return;
        }

        var serializerOptions = context.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value
            .JsonSerializerOptions;
        var body = JsonSerializer.Serialize(
            response,
            response.GetType(),
            serializerOptions);
        var contentType = objectResult.ContentTypes.FirstOrDefault()
            ?? DefaultContentType;
        var cacheValue = JsonSerializer.Serialize(
            new IdempotencyCacheEntry(
                statusCode,
                contentType,
                body));

        await idempotencyService.SetAsync(
            cacheKey,
            cacheValue,
            TimeSpan.FromMinutes(CacheMinutes),
            context.RequestAborted);
    }

    private static string CreateCacheKey(
        HttpContext context,
        string idempotencyKey)
    {
        var userId = context.User.FindFirst("sub")?.Value
            ?? "anonymous";
        var source = string.Join(
            '\n',
            userId,
            context.Request.Method,
            context.Request.Path.Value,
            idempotencyKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));

        return $"idempotency:{Convert.ToHexString(hash)}";
    }
}
