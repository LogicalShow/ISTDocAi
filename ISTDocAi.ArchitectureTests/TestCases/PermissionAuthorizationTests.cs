using System.Security.Claims;
using System.Text.Json;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class PermissionAuthorizationTests
{
    private const string Permission = "ADM_IAM_USR.VIEW";

    [Test]
    public void RequirePermissionAttribute_ShouldAllowMatchingPermission()
    {
        var httpContext = CreateHttpContext(
            new ClaimsIdentity(
                [new Claim("permission", Permission)],
                "Test"));
        var attribute = new RequirePermissionAttribute(Permission);
        var filterContext = CreateAuthorizationFilterContext(httpContext);

        attribute.OnAuthorization(filterContext);

        Assert.That(filterContext.Result, Is.Null);
    }

    [TestCase(
        false,
        StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthUnauthorized,
        ErrorType.Unauthorized)]
    [TestCase(
        true,
        StatusCodes.Status403Forbidden,
        ErrorCodes.AuthForbidden,
        ErrorType.Forbidden)]
    public void RequirePermissionAttribute_ShouldReturnFailureEnvelope(
        bool isAuthenticated,
        int expectedStatusCode,
        string expectedCode,
        ErrorType expectedType)
    {
        var identity = isAuthenticated
            ? new ClaimsIdentity(authenticationType: "Test")
            : new ClaimsIdentity();
        var httpContext = CreateHttpContext(identity);
        var attribute = new RequirePermissionAttribute(Permission);
        var filterContext = CreateAuthorizationFilterContext(httpContext);

        attribute.OnAuthorization(filterContext);

        var objectResult = filterContext.Result as ObjectResult;
        var result = objectResult?.Value as Result;

        Assert.Multiple(() =>
        {
            Assert.That(objectResult?.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(result?.IsFailure, Is.True);
            Assert.That(result?.Error.Code, Is.EqualTo(expectedCode));
            Assert.That(result?.Error.Type, Is.EqualTo(expectedType));
        });
    }

    [TestCase(true, StatusCodes.Status401Unauthorized, ErrorCodes.AuthUnauthorized, "Unauthorized")]
    [TestCase(false, StatusCodes.Status403Forbidden, ErrorCodes.AuthForbidden, "Forbidden")]
    public async Task ApiAuthorizationResultHandler_ShouldReturnResultEnvelope(
        bool challenged,
        int expectedStatusCode,
        string expectedCode,
        string expectedType)
    {
        var services = new ServiceCollection()
            .AddOptions()
            .AddApiJsonSerialization()
            .AddSingleton<IAuthenticationService, TestAuthenticationService>()
            .AddSingleton<IMessageLocalizer, PassThroughMessageLocalizer>()
            .BuildServiceProvider();
        var body = new MemoryStream();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body }
        };
        httpContext.Request.Path = "/api/IAM/User/Get";

        var authorizationResult = challenged
            ? PolicyAuthorizationResult.Challenge()
            : PolicyAuthorizationResult.Forbid();
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        var handler = new ApiAuthorizationMiddlewareResultHandler();

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            httpContext,
            policy,
            authorizationResult);

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(root.GetProperty("IsSuccess").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("Error").GetProperty("Code").GetString(),
                Is.EqualTo(expectedCode));
            Assert.That(root.GetProperty("Error").GetProperty("Type").GetString(),
                Is.EqualTo(expectedType));
        });
    }

    [Test]
    public async Task AuthorizationResultHandler_ShouldNotWrapNonApiResponse()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService, TestAuthenticationService>()
            .BuildServiceProvider();
        var body = new MemoryStream();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body }
        };
        httpContext.Request.Path = "/fastreport/invoice";
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        var handler = new ApiAuthorizationMiddlewareResultHandler();

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            httpContext,
            policy,
            PolicyAuthorizationResult.Challenge());

        Assert.Multiple(() =>
        {
            Assert.That(
                httpContext.Response.StatusCode,
                Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(body.Length, Is.Zero);
        });
    }

    private static DefaultHttpContext CreateHttpContext(
        ClaimsIdentity identity)
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageLocalizer, PassThroughMessageLocalizer>()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(identity)
        };
    }

    private static AuthorizationFilterContext CreateAuthorizationFilterContext(
        HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        return new AuthorizationFilterContext(
            actionContext,
            []);
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;
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
