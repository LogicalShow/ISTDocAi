using ISTDocAi.Application.Common.Interfaces.DocAi.Pricing;
using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Infrastructure.Services.DocAi.Pricing;
namespace ISTDocAi.ArchitectureTests.TestCases;
public sealed class DocAiPricingBoundaryTests
{
    [Test]
    public void Pricing_policy_and_port_are_application_owned_and_http_is_infrastructure_owned()
    {
        Assert.That(typeof(AzurePricePolicy).Assembly,Is.SameAs(Assemblies.Application));
        Assert.That(typeof(IAzureRetailPriceSource).Namespace,Is.EqualTo("ISTDocAi.Application.Common.Interfaces.DocAi.Pricing"));
        Assert.That(typeof(AzureRetailPriceClient).GetInterfaces(),Does.Contain(typeof(IAzureRetailPriceSource)));
        Assert.That(Types.InAssembly(Assemblies.Application).That().ResideInNamespace("ISTDocAi.Application.Services.DocAi.Pricing")
            .ShouldNot().HaveDependencyOnAny("System.Net.Http","ISTDocAi.Infrastructure").GetResult().IsSuccessful,Is.True);
    }
}
