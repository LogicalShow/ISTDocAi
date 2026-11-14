using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class MappingRuntimeBoundaryTests
{
    [TestCase("Sql", typeof(SqlMappingProfileResolver))]
    public void Source_selects_one_singleton_shared_by_both_consumers(string source, Type expected)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DocAi:Mapping:Source"] = source }).Build());
        services.AddLogging();
        services.AddDocAiSql(o => o.ConnectionString = "unused");
        services.AddDocAiMapping();
        services.AddSingleton<ISqlConnectionFactory, FailedConnections>();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMappingProfileResolver>();
        Assert.That(resolver, Is.TypeOf(expected));
        Assert.That(provider.GetRequiredService<IMappingProfileResolver>(), Is.SameAs(resolver));
        Assert.That(provider.GetRequiredService<IAnalysisParser>(), Is.TypeOf<AnalysisParser>());
        Assert.That(provider.GetRequiredService<ModelResultMapper>(), Is.Not.Null);
    }
    [TestCase("Sql")]
    public async Task Only_direct_SQL_translates_preflight_dependency_failure(string source)
    {
        var connections = new FailedConnections();
        var options = Options.Create(new DocAiSqlOptions());
        var sql = new SqlMappingProfileResolver(connections, options, TimeProvider.System);
        IMappingProfileResolver resolver = sql;
        var store = new SqlDocAiStore(connections, new SqlStatementCatalog(), options, null, null, resolver);
        if (source == "Sql")
        {
            var error = await PortAssert.ThrowsAsync<IntakeException>(() => store.IntakeAsync(SqlDatabase.Request(), default));
            Assert.That(error.Status, Is.EqualTo(500));
            Assert.That(error.Code, Is.EqualTo("INTERNAL_ERROR"));
        }
        else await PortAssert.ThrowsAsync<InvalidOperationException>(() => store.IntakeAsync(SqlDatabase.Request(), default));
        Assert.That(connections.Calls, Is.EqualTo(1));
    }
    private sealed class FailedConnections : ISqlConnectionFactory
    {
        public int Calls;
        public Task<SqlConnection> OpenAsync(CancellationToken ct) { Calls++; throw new InvalidOperationException("Test SQL boundary"); }
    }
}
