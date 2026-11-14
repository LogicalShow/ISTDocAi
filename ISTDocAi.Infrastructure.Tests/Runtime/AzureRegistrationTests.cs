using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Azure.Core;
using Azure.Identity;

using ISTDocAi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class AzureRegistrationTests
{
    
    [TestCase("Development", null)]
    [TestCase("Development", "11111111-1111-1111-1111-111111111111")]
    [TestCase("Production", null)]
    [TestCase("Production", "11111111-1111-1111-1111-111111111111")]
    [TestCase("Staging", null)]
    public void Environment_selects_one_shared_credential(string environment, string? clientId)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocAi:DocumentIntelligence:Endpoint"] = "https://docaitest.cognitiveservices.azure.com",
            ["DocAi:Blob:AccountUrl"] = "https://docaitest.blob.core.windows.net",
            ["DocAi:Blob:ManagedIdentityClientId"] = clientId
        }).Build();
        var services = new ServiceCollection().AddDocAiAzure(new TestEnvironment(environment), configuration);
        using var provider = services.BuildServiceProvider();
        var credential = provider.GetRequiredService<TokenCredential>();
        if (environment == Environments.Development) PortAssert.IsType<DefaultAzureCredential>(credential);
        else PortAssert.IsType<ManagedIdentityCredential>(credential);
        PortAssert.IsType<BlobRawResultStore>(provider.GetRequiredService<IRawResultStore>());
        PortAssert.IsType<DocumentIntelligenceRestClient>(provider.GetRequiredService<IDocumentAnalyzer>());
        PortAssert.Same(provider.GetRequiredService<AnalysisRateLimits>(), provider.GetRequiredService<AnalysisRateLimits>());
        PortAssert.Same(provider.GetRequiredService<BearerTokenCache>(), provider.GetRequiredService<BearerTokenCache>());
        using var scope = provider.CreateScope();
        PortAssert.Same(credential, scope.ServiceProvider.GetRequiredService<TokenCredential>());
        PortAssert.IsType<BlobDocumentStore>(provider.GetRequiredService<IDocumentStore>());
        PortAssert.Same(provider.GetRequiredService<IDocumentStore>(), scope.ServiceProvider.GetRequiredService<IDocumentStore>());
    }
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "DocAi.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

