using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class ApiValidationResponseFactoryTests
{
    [Test]
    public void Create_ShouldReturnValidationResultEnvelope()
    {
        const string errorMessage =
            "The JSON value could not be converted to System.Boolean.";

        var services = new ServiceCollection()
            .AddSingleton<IMessageLocalizer, PassThroughMessageLocalizer>()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.IsActive", errorMessage);

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        var actionResult = ApiValidationResponseFactory.Create(actionContext);

        var badRequest = actionResult as BadRequestObjectResult;
        var validationResult = badRequest?.Value as ValidationResult;

        Assert.Multiple(() =>
        {
            Assert.That(badRequest?.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(validationResult, Is.Not.Null);
            Assert.That(validationResult!.IsFailure, Is.True);
            Assert.That(validationResult.Error.Type, Is.EqualTo(ErrorType.Validation));
            Assert.That(validationResult.Errors, Has.Length.EqualTo(1));
            Assert.That(validationResult.Errors[0].Code, Is.EqualTo("$.IsActive"));
            Assert.That(validationResult.Errors[0].Message, Is.EqualTo(errorMessage));
        });
    }

    private sealed class PassThroughMessageLocalizer : IMessageLocalizer
    {
        public string Localize(string key, string fallback, params object[] args) => fallback;

        public Error Localize(Error error) => error;

        public Result Localize(Result result) => result;
    }
}
