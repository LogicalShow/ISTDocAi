using System.Security.Claims;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Idempotency;
using ISTDocAi.UI.Server.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class IdempotencyAttributeTests
{
    [Test]
    public async Task MissingRequiredKey_ShouldReturnValidationEnvelope()
    {
        var service = new TestIdempotencyService();
        var context = CreateExecutingContext(service, includeHeader: false);
        var nextCalled = false;

        await new EnableIdempotencyAttribute().OnActionExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return CreateExecutedContext(
                    context,
                    new OkObjectResult(Result.Success()));
            });

        var badRequest = context.Result as BadRequestObjectResult;
        var result = badRequest?.Value as ValidationResult;

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(
                badRequest?.StatusCode,
                Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result?.IsFailure, Is.True);
            Assert.That(
                result?.Errors.Single().Code,
                Is.EqualTo(ErrorCodes.IdempotencyKeyRequired));
            Assert.That(
                result?.Errors.Single().Type,
                Is.EqualTo(ErrorType.Validation));
        });
    }

    [Test]
    public async Task SuccessfulResult_ShouldCacheCompleteResponse()
    {
        var service = new TestIdempotencyService();
        var context = CreateExecutingContext(service);
        var response = Result.Success(new TestResponse("123"));
        var actionResult = new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status201Created
        };

        await new EnableIdempotencyAttribute().OnActionExecutionAsync(
            context,
            () => CreateExecutedContext(context, actionResult));

        using var cached = JsonDocument.Parse(service.StoredValue!);
        var cachedRoot = cached.RootElement;
        using var body = JsonDocument.Parse(
            cachedRoot.GetProperty("Body").GetString()!);

        Assert.Multiple(() =>
        {
            Assert.That(
                cachedRoot.GetProperty("StatusCode").GetInt32(),
                Is.EqualTo(StatusCodes.Status201Created));
            Assert.That(
                cachedRoot.GetProperty("ContentType").GetString(),
                Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(body.RootElement.GetProperty("IsSuccess").GetBoolean(), Is.True);
            Assert.That(
                body.RootElement.GetProperty("Value").GetProperty("Id").GetString(),
                Is.EqualTo("123"));
            Assert.That(service.StoredKey, Does.StartWith("idempotency:"));
        });
    }

    [Test]
    public async Task CachedResponse_ShouldBypassActionAndPreserveHttpResponse()
    {
        const string cachedBody =
            "{\"IsSuccess\":true,\"Value\":{\"Id\":\"123\"}}";
        var service = new TestIdempotencyService
        {
            CachedValue = JsonSerializer.Serialize(new
            {
                StatusCode = StatusCodes.Status201Created,
                ContentType = "application/json; charset=utf-8",
                Body = cachedBody
            })
        };
        var context = CreateExecutingContext(service);
        var nextCalled = false;

        await new EnableIdempotencyAttribute().OnActionExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return CreateExecutedContext(
                    context,
                    new OkObjectResult(Result.Success()));
            });

        var contentResult = context.Result as ContentResult;

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(
                contentResult?.StatusCode,
                Is.EqualTo(StatusCodes.Status201Created));
            Assert.That(
                contentResult?.ContentType,
                Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(contentResult?.Content, Is.EqualTo(cachedBody));
        });
    }

    [Test]
    public async Task FailedResult_ShouldNotBeCached()
    {
        var service = new TestIdempotencyService();
        var context = CreateExecutingContext(service);
        var response = Result.Failure(
            Error.BusinessRule("Test.BusinessRule", "Business rule failed."));
        var actionResult = new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };

        await new EnableIdempotencyAttribute().OnActionExecutionAsync(
            context,
            () => CreateExecutedContext(context, actionResult));

        Assert.That(service.StoredValue, Is.Null);
    }

    private static ActionExecutingContext CreateExecutingContext(
        IIdempotencyService idempotencyService,
        bool includeHeader = true)
    {
        var services = new ServiceCollection()
            .AddOptions()
            .AddApiJsonSerialization()
            .AddSingleton(idempotencyService)
            .AddSingleton<IMessageLocalizer, PassThroughMessageLocalizer>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("sub", "user-1")],
                    "Test"))
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/IAM/User/Add";
        if (includeHeader)
        {
            httpContext.Request.Headers["Idempotency-Key"] = "request-1";
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }

    private static Task<ActionExecutedContext> CreateExecutedContext(
        ActionExecutingContext executingContext,
        IActionResult result) =>
        Task.FromResult(
            new ActionExecutedContext(
                executingContext,
                [],
                executingContext.Controller)
            {
                Result = result
            });

    private sealed record TestResponse(string Id);

    private sealed class TestIdempotencyService : IIdempotencyService
    {
        public string? CachedValue { get; init; }
        public string? StoredKey { get; private set; }
        public string? StoredValue { get; private set; }

        public Task<string?> GetAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CachedValue);

        public Task SetAsync(
            string key,
            string value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            StoredKey = key;
            StoredValue = value;
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughMessageLocalizer : IMessageLocalizer
    {
        public string Localize(
            string key,
            string fallback,
            params object[] args) => fallback;

        public Error Localize(Error error) => error;

        public Result Localize(Result result) => result;
    }
}
