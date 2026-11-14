using System.Net;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests;
[NonParallelizable]
public sealed class MappingReadinessHostTests
{
    [TestCase("Sql", true, 503, 1)]
    [TestCase("Sql", false, 503, 0)]
    public async Task Readiness_checks_mapping_only_after_database_and_SQL_failure_is_cached(string source, bool databaseReady, int status, int calls)
    {
        var connections = new FailedConnections();
        using var factory = new BackendFactory(false);
        using var configured = factory.WithWebHostBuilder(b => {
            b.UseSetting("DocAi:Mapping:Source", source);
            b.UseSetting("DocAi:Blob:AccountUrl", "https://test.blob.core.windows.net");
            b.ConfigureTestServices(s => {
                var readiness = new DatabaseReadiness();
                if (databaseReady) readiness.MarkReady();
                s.RemoveAll<IDatabaseReadiness>(); s.AddSingleton<IDatabaseReadiness>(readiness);
                s.RemoveAll<ISqlConnectionFactory>(); s.AddSingleton<ISqlConnectionFactory>(connections);
            });
        });
        using var client = configured.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            using var response = await client.GetAsync("/health/ready");
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
        }
        using var live = await client.GetAsync("/health/live");
        Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(connections.Calls, Is.EqualTo(calls));
    }
    private sealed class FailedConnections : ISqlConnectionFactory
    {
        public int Calls;
        public Task<SqlConnection> OpenAsync(CancellationToken ct) { Calls++; throw new InvalidOperationException("No SQL in host tests"); }
    }
}
