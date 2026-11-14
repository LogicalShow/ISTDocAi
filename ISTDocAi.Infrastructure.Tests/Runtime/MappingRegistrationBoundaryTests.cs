using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class MappingRegistrationBoundaryTests
{
    [Test]
    public void Sql_core_default_does_not_mask_the_later_mapping_source_registration()
    {
        var services = new ServiceCollection();
        ISTDocAi.Application.DependencyInjection.AddApplication(services);
        services.AddDocAiSql(o => o.ConnectionString = "Server=test;Database=test");
        // Stage 5b uses TryAdd, as the original Azure registration does.
        services.TryAddSingleton<IMappingProfileResolver, Resolver>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var resolver = provider.GetRequiredService<IMappingProfileResolver>();
        Assert.That(resolver, Is.TypeOf<Resolver>());
        var repository = scope.ServiceProvider.GetRequiredService<SqlJobRepository>();
        Assert.That(typeof(SqlJobRepository).GetField("profiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(repository), Is.SameAs(resolver));
    }

    private sealed class Resolver : IMappingProfileResolver
    {
        public AnalyzeResultMappingOptions Resolve(string modelId, string documentType, bool allowLegacyDocumentType = false) => new();
    }
}
