using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Microsoft.Extensions.Logging;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.Data.SqlClient;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class SqlOnlyMappingStartupTests
{
    [TestCase("AppSettings")]
    [TestCase("Shadow")]
    [TestCase("Unknown")]
    [TestCase("sql")]
    public void Stale_source_fails_startup_with_fixed_message(string source)
    {
        using var host = Build(source);
        var error = Assert.ThrowsAsync<OptionsValidationException>(async () => await host.StartAsync());
        Assert.That(error!.Failures, Is.EqualTo(new[] { "Mapping profiles come from SQL only." }));
    }
    [TestCase(null)]
    [TestCase("")]
    [TestCase("Sql")]
    public async Task Absent_empty_or_Sql_starts_and_resolves_one_SQL_singleton(string? source)
    {
        using var host = Build(source);
        await host.StartAsync();
        var resolver = host.Services.GetRequiredService<IMappingProfileResolver>();
        Assert.That(resolver, Is.TypeOf<SqlMappingProfileResolver>());
        Assert.That(resolver, Is.SameAs(host.Services.GetRequiredService<SqlMappingProfileResolver>()));
        Assert.That(host.Services.GetRequiredService<IAnalysisParser>(), Is.TypeOf<AnalysisParser>());
        Assert.That(host.Services.GetRequiredService<ModelResultMapper>(), Is.Not.Null);
        await host.StopAsync();
    }
    private static IHost Build(string? source)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DocAi:Mapping:Source"] = source,
            ["DocAi:Mapping:FieldMap:ignored"] = "Ignored",
            ["DocAi:Mapping:Models:ignored:DocumentTypeCode"] = "BL" });
        builder.Services.AddDocAiSql(o => o.ConnectionString = "unused");
        builder.Services.AddDocAiMapping();
        builder.Services.AddSingleton<ISqlConnectionFactory, DeniedConnections>();
        return builder.Build();
    }

    [TestCase(null)]
    [TestCase("Sql")]
    public async Task Readiness_always_checks_SQL_and_caches_failure_without_a_source_option(string? source)
    {
        using var host = Build(source);
        await host.StartAsync();
        var readiness = host.Services.GetRequiredService<MappingProfileReadiness>();
        Assert.That(await readiness.CheckAsync(default), Is.False);
        Assert.That(await readiness.CheckAsync(default), Is.False);
        Assert.That(((DeniedConnections)host.Services.GetRequiredService<ISqlConnectionFactory>()).Calls, Is.EqualTo(1));
        await host.StopAsync();
    }

    private sealed class DeniedConnections : ISqlConnectionFactory
    {
        public int Calls;
        public Task<SqlConnection> OpenAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("Offline SQL boundary");
        }
    }
}
