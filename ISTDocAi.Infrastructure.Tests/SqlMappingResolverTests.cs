using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using System.Collections.Concurrent;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class SqlMappingResolverTests
{
    private static readonly Dictionary<string, string> Hashes = new()
    {
        ["bl-maersk-v1"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["bl-v2"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["bl-v3"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["manifest-v1"] = "87aa5155640c0e10dc2379621e36c290a7bc16905d6e5f201cd55466f3a8ef2d",
        ["manifest-v2"] = "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480",
        ["manifest-v3"] = "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480",
        ["extra-bl"] = "c9a1b76d2f79ff08355d51d2d347238d803d5370f8c9a21988b27ba578ed0d17"
    };

    [Test, Category("Sql")]
    public async Task Local_and_iis_registries_match_frozen_hashes_and_real_configuration_with_one_cold_command()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = timeout.Token;
        foreach (var local in new[] { true, false })
        {
            await using var db = new DisposableDatabase();
            await db.Create(ct);
            await using (var context = db.Context())
                await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", ct);
            var models = local ? Hashes.Keys.ToArray() : ["bl-v3", "manifest-v3"];
            var version = 0;
            foreach (var model in models)
                await db.Execute($"INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('{model}','extract','{Type(model)}',{++version},'Draft')", ct);
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('router','classify','AUTO',1,'Draft');", ct);
            await using (var context = db.Context()) await context.Database.MigrateAsync(ct);
            var options = Options.Create(new DocAiSqlOptions { ConnectionString = db.ConnectionString });
            var factory = new CountingFactory(new SqlConnectionFactory(options));
            var clock = new Clock();
            var sql = new SqlMappingProfileResolver(factory, options, clock);
            var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build();
            var app = new InMemoryMappingProfileResolver(Options.Create(configuration.GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!));
            foreach (var model in models)
            {
                var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => sql.Resolve(model, Type(model)), ct)));
                Assert.All(results, profile => Assert.Equal(Hashes[model], MappingFingerprint.ComputeEffective(profile, Type(model))));
                Assert.Equal(models.TakeWhile(m => m != model).Count() + 1, factory.Opens);
                Assert.Equal(Hashes[model], MappingFingerprint.ComputeEffective(app.Resolve(model, Type(model)), Type(model)));
                results[0].FieldMap.Clear();
                Assert.Equal(Hashes[model], MappingFingerprint.ComputeEffective(sql.Resolve(model, Type(model)), Type(model)));
            }
            Assert.Equal(models.Length, factory.Opens);
            Assert.Equal(models.Length, factory.Commands);
            clock.Advance(61);
            _ = sql.Resolve(models[0], Type(models[0]));
            Assert.Equal(models.Length + 1, factory.Commands);

            // Register after migration so this BL is genuinely unbound, despite legacy appsettings fallback.
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('unbound','extract','BL',100,'Draft');", ct);
            Assert.Throws<OptionsValidationException>(() => sql.Resolve("unbound", "BL"));
            var count = factory.Commands;
            Assert.Throws<OptionsValidationException>(() => sql.Resolve("unbound", "BL"));
            Assert.Equal(count, factory.Commands);
            Assert.Throws<OptionsValidationException>(() => sql.Resolve(models[0], "MANIFEST"));
            clock.Advance(6);
            Assert.Throws<OptionsValidationException>(() => sql.Resolve("unbound", "BL"));
            Assert.Equal(count + 1, factory.Commands);
        }
    }
    private static string Type(string model) => model.StartsWith("manifest", StringComparison.Ordinal) ? "MANIFEST" : "BL";
    private static async Task Until(Func<bool> done, CancellationToken ct)
    { while (!done()) await Task.Delay(10, ct); }
    private sealed class CountingFactory(ISqlConnectionFactory inner) : ISqlConnectionFactory
    {
        private readonly ConcurrentBag<SqlConnection> connections = [];
        public int Opens => connections.Count;
        public long Commands => connections.Sum(c => Convert.ToInt64(c.RetrieveStatistics()["SelectCount"], System.Globalization.CultureInfo.InvariantCulture));
        public SqlConnection Open()
        {
            var connection = inner.Open();
            connection.StatisticsEnabled = true;
            connections.Add(connection);
            return connection;
        }
        public async Task<SqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = await inner.OpenAsync(ct);
            connection.StatisticsEnabled = true;
            connections.Add(connection);
            return connection;
        }
    }
    private sealed class Clock : TimeProvider
    {
        private long seconds;
        private int reads;
        public int Reads => Volatile.Read(ref reads);
        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Increment(ref reads);
            return DateTimeOffset.UnixEpoch.AddSeconds(Interlocked.Read(ref seconds));
        }
        public void Advance(int value) => Interlocked.Add(ref seconds, value);
    }
}
