using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Collections.Concurrent;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class SqlMappingCacheTests
{
    
    [TestCase("throw")]
    [TestCase("missing")]
    [TestCase("malformed")]
    public async Task Failed_refresh_retains_immutable_snapshot_coalesces_and_recovers(string failure)
    {
        var clock = new TestClock();
        var log = new CaptureLog();
        var calls = 0;
        var fail = false;
        var resolver = new SqlMappingProfileResolver(clock, model =>
        {
            Interlocked.Increment(ref calls);
            if (!fail) return Rows(model);
            return failure switch
            {
                "missing" => [],
                "malformed" => [new() { MappingShape = "INVALID" }],
                _ => throw new InvalidOperationException("PRIVATE SQL DATA")
            };
        }, log);
        var original = resolver.Resolve("m", "BL");
        var fingerprint = MappingFingerprint.ComputeEffective(original, "BL");
        original.FieldMap.Clear();
        fail = true;
        clock.Advance(TimeSpan.FromSeconds(60));
        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() =>
            Assert.Equal(fingerprint, MappingFingerprint.ComputeEffective(resolver.Resolve("m", "BL"), "BL")))));
        Assert.Equal(2, calls);
        Assert.Single(log.Events);
        Assert.Null(log.Events.Single().Error);
        Assert.Equal(new[] { "event", "modelId" }, log.Events.Single().State.Keys.Where(k => k != "{OriginalFormat}").Order());
        Assert.Equal("mapping.refresh_failed", log.Events.Single().State["event"]);
        clock.Advance(TimeSpan.FromSeconds(29));
        _ = resolver.Resolve("m", "BL");
        Assert.Equal(2, calls);
        clock.Advance(TimeSpan.FromSeconds(1));
        _ = resolver.Resolve("m", "BL");
        Assert.Equal(3, calls);
        Assert.Equal(2, log.Events.Count);
        fail = false;
        clock.Advance(TimeSpan.FromSeconds(30));
        _ = resolver.Resolve("m", "BL");
        Assert.Equal(4, calls);
        clock.Advance(TimeSpan.FromSeconds(59));
        _ = resolver.Resolve("m", "BL");
        Assert.Equal(4, calls);
        Assert.Throws<OptionsValidationException>(() => resolver.Resolve("m", "MANIFEST"));
    }

    [Test]
    public void No_snapshot_fails_and_missing_binding_uses_short_negative_cache()
    {
        var clock = new TestClock();
        var calls = 0;
        var fail = false;
        var resolver = new SqlMappingProfileResolver(clock, _ =>
        {
            calls++;
            if (fail) throw new InvalidOperationException("PRIVATE SQL DATA");
            return [];
        });
        Assert.Throws<OptionsValidationException>(() => resolver.Resolve("m", "BL"));
        Assert.Throws<OptionsValidationException>(() => resolver.Resolve("m", "BL"));
        Assert.Equal(1, calls);
        fail = true;
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("m", "BL"));
        Assert.Equal(2, calls);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("new", "BL"));
    }

    
    [TestCase("Sql")]
    public async Task Readiness_bypasses_other_modes_and_caches_SQL_failures(string source)
    {
        var clock = new TestClock();
        var factory = new FailedConnections();
        using var readiness = new MappingProfileReadiness(factory, Options.Create(new DocAiSqlOptions()),
            clock, NullLogger<MappingProfileReadiness>.Instance);
        var expected = false;
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ => Assert.Equal(expected, await readiness.CheckAsync(default))));
        Assert.Equal(expected ? 0 : 1, factory.Calls);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(expected, await readiness.CheckAsync(default));
        Assert.Equal(expected ? 0 : 2, factory.Calls);
    }

    [Test]
    public async Task Intake_preflight_dependency_failure_uses_existing_500_before_any_other_connection()
    {
        var connections = new FailedConnections();
        var options = Options.Create(new DocAiSqlOptions());
        var resolver = new SqlMappingProfileResolver(connections, options, new TestClock());
        var store = new SqlDocAiStore(connections, new SqlStatementCatalog(), options, null, null, resolver);
        var error = await Assert.ThrowsAsync<IntakeException>(() => store.IntakeAsync(SqlDatabase.Request(), default));
        Assert.Equal(500, error.Status);
        Assert.Equal("INTERNAL_ERROR", error.Code);
        Assert.DoesNotContain("PRIVATE", error.Message);
        Assert.Equal(1, connections.Calls);
    }

    private static SqlMappingProfileResolver.Row[] Rows(string model) =>
        [new() { ModelId = model, DocumentTypeCode = "BL", MappingShape = "LegacyFallback",
            ContainersField = "containers", ContainerNumberField = "containerNumber", SealNumberField = "sealNumber",
            MapKind = "Field", CanonicalKey = "shipperName", AzureName = "Shipper" }];
    private sealed class FailedConnections : ISqlConnectionFactory
    {
        public int Calls;
        public SqlConnection Open() { Interlocked.Increment(ref Calls); throw new InvalidOperationException("PRIVATE SQL DATA"); }
        public Task<SqlConnection> OpenAsync(CancellationToken ct) { Interlocked.Increment(ref Calls); throw new InvalidOperationException("PRIVATE SQL DATA"); }
    }
    private sealed class CaptureLog : ILogger<SqlMappingProfileResolver>
    {
        public ConcurrentQueue<(Dictionary<string, object?> State, Exception? Error)> Events { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) =>
            Events.Enqueue((((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(p => p.Key, p => p.Value), error));
    }
}
