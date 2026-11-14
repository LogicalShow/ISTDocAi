using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class RoutePolicyTests
{
    [TestCase("AUTO", "BL", null, true)]
    [TestCase("AUTO", "MANIFEST", null, true)]
    [TestCase("BL", "BL", "MAERSK", true)]
    [TestCase("BL", "BL", null, true)]
    [TestCase("INV", "INV", "LINE", true)]
    [TestCase("AUTO", null, null, false)]
    [TestCase("AUTO", "AUTO", null, false)]
    [TestCase("AUTO", "INV", null, false)]
    [TestCase("BL", "MANIFEST", null, false)]
    public void Routing_accepts_generic_models_but_never_changes_an_explicit_request(string requested, string? target, string? line, bool accepted)
    {
        var route = DocAiServices.ClassifierPolicyService.ResolveRoute(requested, target, line, .8, .8);
        Assert.That(route is not null, Is.EqualTo(accepted));
        if (route is not null)
        {
            Assert.That(route.DocumentTypeCode, Is.EqualTo(target));
            Assert.That(route.ShippingLineCode, Is.EqualTo(line));
        }
    }
}
