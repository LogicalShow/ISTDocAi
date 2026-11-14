using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Text.RegularExpressions;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class CatalogTests
{
    [Test]
    public async Task Every_expected_label_occurs_once_and_slices_are_the_exact_reference_text()
    {
        string[] expected = ["S1","S2","S3","S4a","S4b","S4c","S5","S6","S7","S8a","S8","S9","S10","S11","S12","S13","S14","S15","S16","S17","S18","S19","S20","S21","S23","S24","S22","S25","S26","S27","S28","S29","S30","S31"];
        var catalog = new SqlStatementCatalog();
        Assert.Equal(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "03_docai_statements.sql")), catalog.Source);
        Assert.Equal(expected.Append("B9"), catalog.Statements.Keys);
        Assert.DoesNotContain("S4d", catalog.Statements.Keys);
        foreach (var label in expected)
            Assert.Single(Regex.Matches(catalog.Source, @"(?m)^-- \[" + label + @"\]"));
        var start = catalog.Source.IndexOf("-- [S1]", StringComparison.Ordinal);
        Assert.Equal(catalog.Source[start..], string.Concat(catalog.Statements.Where(x => x.Key != "B9").Select(x => x.Value)));
    }
    [Test]
    public async Task Only_B9_is_loaded_from_the_unmodified_billing_reference()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "04_docai_billing.sql"));
        var catalog = new SqlStatementCatalog();
        Assert.Equal(source[source.IndexOf("-- [B9]", StringComparison.Ordinal)..], catalog["B9"]);
        Assert.Equal(new[] { "B9" }, catalog.Statements.Keys.Where(x => x.StartsWith('B')));
    }
    [Test]
    public void Registration_resolves_all_ports_to_the_same_scoped_adapter()
    {
        var services = new ServiceCollection();
        ISTDocAi.Application.DependencyInjection.AddApplication(services);
        services.AddDocAiSql(o => o.ConnectionString = "Server=test;Database=test");
        services.AddSingleton<IMappingProfileResolver>(new InMemoryMappingProfileResolver());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var execution = scope.ServiceProvider.GetRequiredService<SqlExecution>();
        Type[] ports = [typeof(IJobRepository),typeof(IJobQueue),typeof(IOperationStore),typeof(IWebhookOutbox),typeof(IModelCatalog),typeof(IRetention),typeof(IConsumerSource),typeof(IUsageReader),typeof(IModelThresholdSource),typeof(ICallbackSource),typeof(ISignalReader)];
        foreach (var port in ports)
        {
            var repository = scope.ServiceProvider.GetRequiredService(port);
            Assert.Same(repository, scope.ServiceProvider.GetRequiredService(port));
            Assert.Same(execution, repository.GetType().GetField("execution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(repository));
            using var other = provider.CreateScope();
            Assert.NotSame(repository, other.ServiceProvider.GetRequiredService(port));
            Assert.NotSame(execution, other.ServiceProvider.GetRequiredService<SqlExecution>());
        }
    }
}
