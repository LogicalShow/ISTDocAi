using ISTDocAi.Application.Services.DocAi.Processing;
using ProcessingWorkerFakes = ISTDocAi.Infrastructure.Tests.DocAi.Worker.WorkerFakes;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;


using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class SqlMappingStageFourTests
{
    private static Task MissingSql() { SqlTestGuard.Require(); return Task.CompletedTask; }
    private readonly SqlDatabase db = new();
    private readonly TestClock clock = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL"))
        ? MissingSql() : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private IOptions<DocAiSqlOptions> SqlOptions => Options.Create(new DocAiSqlOptions { ConnectionString = db.ConnectionString });
    private static IOptions<AnalyzeResultMappingOptions> AppOptions => Options.Create(new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build()
        .GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!);
    private SqlDocAiStore Store(IMappingProfileResolver resolver) =>
        new(new SqlConnectionFactory(SqlOptions), new SqlStatementCatalog(), SqlOptions, AppOptions, null, resolver);
    private SqlMappingProfileResolver Resolver(ISqlConnectionFactory? factory = null) =>
        new(factory ?? new SqlConnectionFactory(SqlOptions), SqlOptions, clock);
    private async Task Bind(string model, string seed)
    {
        // Clone an unsealed seed, bind while Draft, then activate. Existing immutable rows are never modified.
        await db.Execute("""
            UPDATE dbo.ModelRegistry SET Status='Draft',ActivatedAtUtc=NULL WHERE ModelId=@model;
            INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,
                ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference)
            SELECT @model+'-stage4',1,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,
                SealNumberField,BillsOfLadingField,'Stage 4 test' FROM dbo.MappingProfiles WHERE ProfileCode=@seed;
            DECLARE @profile bigint=SCOPE_IDENTITY();
            INSERT dbo.MappingFields SELECT @profile,MapKind,CanonicalKey,AzureName FROM dbo.MappingFields
                WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode=@seed);
            INSERT dbo.ModelMappingBindings VALUES(@model,@profile);
            UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId=@model;
            """, new { model, seed });
    }
    private sealed class Registry(SqlDocAiStore store) : ICatalogReader, IModelThresholdReader
    {
        public Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct) => store.GetCatalogAsync(ct);
        public Task<decimal?> ReadThresholdAsync(string modelId, CancellationToken ct) => store.GetThresholdAsync(modelId, ct);
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Unbound_or_unavailable_profile_fails_intake_before_any_write(bool unavailable)
    {
        if (unavailable) await Bind("bl-v1", "legacy");
        var resolver = Resolver(unavailable ? new FailedColdLoad(new SqlConnectionFactory(SqlOptions)) : null);
        var error = await Assert.ThrowsAsync<IntakeException>(() => Store(resolver).IntakeAsync(SqlDatabase.Request(), default));
        Assert.Equal(500, error.Status);
        Assert.Equal("INTERNAL_ERROR", error.Code);
        Assert.DoesNotContain("PRIVATE", error.Message);
        Assert.Equal(0, await db.Scalar<int>("SELECT (SELECT COUNT(*) FROM dbo.Jobs)+(SELECT COUNT(*) FROM dbo.JobRuns)+(SELECT COUNT(*) FROM dbo.PageReservations)+(SELECT COUNT(*) FROM dbo.UsageLedger)+(SELECT COUNT(*) FROM dbo.CustomerPageCharges)"));
    }

    [Test]
    public async Task Readiness_is_false_for_unbound_active_model_then_true_after_binding_and_cache_expiry()
    {
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='inv-line'");
        using var readiness = new MappingProfileReadiness(new SqlConnectionFactory(SqlOptions), SqlOptions,
            clock, NullLogger<MappingProfileReadiness>.Instance);
        Assert.False(await readiness.CheckAsync(default));
        await Bind("bl-v1", "legacy");
        Assert.False(await readiness.CheckAsync(default));
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(await readiness.CheckAsync(default));
    }

    [Test]
    public async Task SQL_refresh_failure_serves_previous_snapshot_but_cold_model_fails()
    {
        await Bind("bl-v1", "legacy");
        var factory = new FailedColdLoad(new SqlConnectionFactory(SqlOptions)) { Fail = false };
        var resolver = Resolver(factory);
        var fingerprint = MappingFingerprint.ComputeEffective(resolver.Resolve("bl-v1", "BL"), "BL");
        factory.Fail = true;
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(fingerprint, MappingFingerprint.ComputeEffective(resolver.Resolve("bl-v1", "BL"), "BL"));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("cold-model", "BL"));
    }
    private sealed class FailedColdLoad(ISqlConnectionFactory inner) : ISqlConnectionFactory
    {
        public Task<SqlConnection> OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);
        public bool Fail { get; set; } = true;
        public SqlConnection Open() => Fail ? throw new InvalidOperationException("PRIVATE SQL DATA") : inner.Open();
    }
    private async Task Process(SqlDocAiStore store, IMappingProfileResolver resolver, ClaimedJob job, string type)
    {
        var fake = new ProcessingWorkerFakes
        {
            MappingParser = new AnalysisParser(resolver),
            Raw = type == "BL" ? """{"pages":[{}],"documents":[{"fields":{"ShipperName":{"valueString":"Fixture shipper","confidence":0.99}}}]}"""
                : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", "test-A-kestrel-seen.analyze.json"))
        };
        await fake.Processor(store, store, store, new Registry(store), new Registry(store)).ExecuteAsync(job, default);
        Assert.Equal("Completed", (await store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
    }

    
    [TestCase("BL", "bl-v1", "legacy")]
    [TestCase("MANIFEST", "manifest-v1", "manifest-v1")]
    public async Task Worker_result_bytes_and_fingerprint_match_and_cutover_does_not_rerun_or_charge(string type, string model, string seed)
    {
        if (type == "MANIFEST") await db.Execute("""
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status)
            VALUES('manifest-v1','MANIFEST','extract',1,'Draft');
            """);
        await Bind(model, seed);
        var app = new InMemoryMappingProfileResolver(AppOptions);
        var appStore = Store(app);
        var request = SqlDatabase.Request(type);
        await appStore.IntakeAsync(request, default);
        var job = (await SqlClaimTestHelper.ExpectedAsync(appStore.ClaimAsync));
        await Process(appStore, app, job, type);
        var before = await db.Scalar<string>("SELECT ResultJson FROM dbo.JobResults WHERE JobId=@JobId", job);
        var fingerprint = await db.Scalar<string>("SELECT MappingFingerprint FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=@RunNo", job);
        var ledger = await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger");
        var charges = await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges");
        var sql = Resolver();
        var sqlStore = Store(sql);
        var replay = await sqlStore.IntakeAsync(request with { ClientRequestId = "sql-cutover" }, default);
        Assert.Equal(IntakeOutcome.Existing, replay.Outcome);
        Assert.Equal(job.JobId, replay.JobId);
        Assert.Equal((short)1, replay.RunNo);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"));
        Assert.Equal(ledger, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(charges, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
        await sqlStore.IntakeAsync(SqlDatabase.Request(type), default);
        var sqlJob = (await SqlClaimTestHelper.ExpectedAsync(sqlStore.ClaimAsync));
        await Process(sqlStore, sql, sqlJob, type);
        Assert.Equal(before, await db.Scalar<string>("SELECT ResultJson FROM dbo.JobResults WHERE JobId=@JobId", sqlJob));
        Assert.Equal(fingerprint, await db.Scalar<string>("SELECT MappingFingerprint FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=@RunNo", sqlJob));
    }

    [Test]
    public async Task In_flight_map_cold_loads_permanent_binding_after_producing_model_is_retired()
    {
        await Bind("bl-v1", "legacy");
        var job = await db.NewClaim();
        var operation = await db.Accepted(job);
        await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2,
            clock.GetUtcNow(), "raw-path"), default);
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired',RetiredAtUtc=SYSUTCDATETIME() WHERE ModelId='bl-v1'");
        var resolver = Resolver(); // No warm cache can hide an incorrect Active filter.
        await Process(Store(resolver), resolver, job, "BL");
        Assert.Equal(MappingFingerprint.ComputeEffective(new InMemoryMappingProfileResolver(AppOptions).Resolve("bl-v1", "BL"), "BL"),
            await db.Scalar<string>("SELECT MappingFingerprint FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=@RunNo", job));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
    }
}
