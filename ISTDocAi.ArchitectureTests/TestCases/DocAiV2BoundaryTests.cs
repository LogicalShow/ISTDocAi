using System.Reflection;
using ISTDocAi.Application.Common.Behaviors;
using ISTDocAi.Application.Features.DocAi.Jobs.Commands.SubmitJob;
using ISTDocAi.Application.Features.DocAi.Catalog.Queries.GetCatalog;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Controllers.DocAi;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
namespace ISTDocAi.ArchitectureTests.TestCases;
public sealed class DocAiV2BoundaryTests
{
    [Test]
    public void DocAi_commands_are_explicitly_excluded_from_request_payload_logging()
    {
        var behavior = typeof(RequestLoggingBehavior<SubmitJobCommand,Result<SubmitJobCommandResponse>>);
        var shouldSkip = (bool)behavior.GetMethod("ShouldSkipLogging",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
        Assert.That(shouldSkip, Is.True);
    }
    [TestCase(typeof(JobsController))] [TestCase(typeof(CatalogController))] [TestCase(typeof(UsageController))]
    public void V2_controllers_require_only_the_API_key_scheme(Type controller)
    {
        var attributes = controller.GetCustomAttributes<AuthorizeAttribute>(true).ToArray();
        Assert.That(attributes, Has.Length.EqualTo(1));
        Assert.That(attributes[0].AuthenticationSchemes, Is.EqualTo(DocAiApiKeyHandler.SchemeName));
        Assert.That(controller.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null);
        Assert.That(controller.GetMethods().Any(m => m.IsDefined(typeof(AllowAnonymousAttribute))), Is.False);
    }
    [TestCase(typeof(SubmitJobCommandResponse))] [TestCase(typeof(GetCatalogResponse))]
    [TestCase(typeof(ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob.GetJobResponse))] [TestCase(typeof(ISTDocAi.Application.Features.DocAi.Usage.Queries.GetUsage.GetUsageResponse))] [TestCase(typeof(ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetTopUpCycles.GetTopUpCyclesResponse))] [TestCase(typeof(ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetCycleDocuments.GetCycleDocumentsResponse))]
    public void V2_DTOs_do_not_expose_cost_or_meter_fields(Type type)
    {
        var seen = new HashSet<Type>(); var pending = new Stack<Type>(); pending.Push(type);
        while (pending.TryPop(out var current))
        {
            if (!seen.Add(current)) continue;
            foreach (var p in current.GetProperties())
            {
                Assert.That(p.Name.ToLowerInvariant(), Does.Not.Contain("usd").And.Not.Contain("azurecost").And.Not.Contain("meter").And.Not.Contain("dollar"));
                var child = p.PropertyType.IsArray ? p.PropertyType.GetElementType()! : p.PropertyType;
                foreach (var arg in child.GetGenericArguments())
                    if (arg.Namespace?.StartsWith("ISTDocAi.",StringComparison.Ordinal)==true) pending.Push(arg);
                if (child.Namespace?.StartsWith("ISTDocAi.",StringComparison.Ordinal)==true) pending.Push(child);
            }
        }
    }
}
