using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Common.Mappings.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ErrorCodes = ISTDocAi.Domain.Common.Constants.DocAi.ErrorCodes;

using ISTDocAi.Infrastructure;
using ProcessingWorkerFakes = ISTDocAi.Infrastructure.Tests.DocAi.Worker.WorkerFakes;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class WorkerSqlIntegrationTests
{
    private static Task MissingSql() { SqlTestGuard.Require(); return Task.CompletedTask; }
    private readonly SqlDatabase db = new();
    private readonly ProcessingWorkerFakes f = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? MissingSql() : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private ProcessJob Processor() => f.Processor(db.Store, db.Store, db.Store, new Registry(db.Store), new Registry(db.Store));
    private sealed class Registry(SqlDocAiStore store) : ICatalogReader, IModelThresholdReader
    {
        public Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct) => store.GetCatalogAsync(ct);
        public Task<decimal?> ReadThresholdAsync(string id, CancellationToken ct) => store.GetThresholdAsync(id, ct);
    }
    private async Task<ClaimedJob> TakeOver(ClaimedJob job)
    {
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", job);
        return await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync);
    }
    private async Task<ClaimedJob> DueAgain(ClaimedJob job)
    {
        await db.Execute("UPDATE dbo.Jobs SET NextAttemptAtUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", job);
        return await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync);
    }
    [Test]
    public async Task Real_expiry_after_crash_allows_one_owner_and_resumes_Accepted_without_POST()
    {
        var job = await db.NewClaim();
        await db.Accepted(job);
        var seconds = await db.Scalar<int>("SELECT DATEDIFF(second,SYSUTCDATETIME(),LeaseExpiresAtUtc) FROM dbo.Jobs WHERE JobId=@JobId", job);
        Assert.True(seconds is >= 58 and <= 60);
        Assert.Null(await db.Store.ClaimAsync(default));
        // Simulate a dead process: no heartbeat, release, or mutation of the expiry timestamp.
        await Task.Delay(TimeSpan.FromSeconds(61));
        var claims = await Task.WhenAll(db.Store.ClaimAsync(default), db.Store.ClaimAsync(default));
        Assert.Equal(1, claims.Count(c => c is not null));
        var next = claims.Single(c => c is not null)!;
        Assert.Equal(job.JobId, next.JobId);
        Assert.Equal(job.TakeoverCount + 1, next.TakeoverCount);
        Assert.Equal(WriteOutcome.LeaseLost, await db.Store.RenewAsync(new(job.JobId, job.LeaseToken), default));
        await Processor().ExecuteAsync(next, default);
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations"));
    }
    [Test]
    public async Task Crash_after_saved_202_resumes_polling_without_second_POST()
    {
        var job = await db.NewClaim(); var id = await db.Accepted(job);
        await Processor().ExecuteAsync(await TakeOver(job), default);
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations"));
        Assert.Equal("Succeeded", await db.Scalar<string>("SELECT State FROM dbo.AzureOperations WHERE OperationId=@id", new { id }));
    }
    [Test]
    public async Task Crash_between_POST_and_location_records_unknown_then_exactly_one_more_POST()
    {
        var job = await db.NewClaim();
        await db.Store.BeginAsync(new(job.JobId, job.LeaseToken), "extract", "bl-v1", default);
        // Azure accepted, but the process died before S4b saved the location.
        var permit = await f.ReserveSubmitAsync(default);
        await f.SubmitAsync(permit, ISTDocAi.Domain.Common.Enums.DocAi.OperationKind.Extract, "bl-v1", new byte[] { 1 }, default);
        var next = await TakeOver(job); await Processor().ExecuteAsync(next, default);
        Assert.Equal("Unknown", await db.Scalar<string>("SELECT State FROM dbo.AzureOperations"));
        Assert.Equal(1, await db.Scalar<int>("SELECT Attempts FROM dbo.Jobs"));
        await Processor().ExecuteAsync(await DueAgain(job), default);
        Assert.Equal(2, f.Calls.Count(c => c.StartsWith("post:")));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE State='Unknown'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
    }
    [Test]
    public async Task Two_unknown_calls_refuse_third_POST_and_fail_RETRIES_EXHAUSTED()
    {
        var job = await db.NewClaim(); f.Submit = new SubmitResult.NoAnswer();
        await Processor().ExecuteAsync(job, default);
        await Processor().ExecuteAsync(await DueAgain(job), default);
        await Processor().ExecuteAsync(await DueAgain(job), default);
        Assert.Equal(2, f.Calls.Count(c => c.StartsWith("post:")));
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE State='Unknown'"));
        Assert.Equal("RETRIES_EXHAUSTED", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.ErrorCode);
    }
    [Test]
    public async Task Mapping_crash_resumes_raw_result_with_one_ledger_row_and_retired_model_threshold()
    {
        var job = await db.NewClaim(); f.ParseError = true;
        await db.Execute("UPDATE dbo.ModelRegistry SET MinFieldConfidence=0.80 WHERE ModelId='bl-v1'");
        await Processor().ExecuteAsync(job, default);
        var position = await db.Store.GetPositionAsync(job.JobId, default);
        Assert.Equal("Map", position!.Stage); Assert.Equal("bl-v1", position.MapModelId);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        await db.Execute("""
            UPDATE dbo.ModelRegistry SET Status='Retired',RetiredAtUtc=SYSUTCDATETIME() WHERE ModelId='bl-v1';
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,MinFieldConfidence)
            VALUES('bl-v2','BL','extract',2,'Active',0.99);
            """);
        f.ParseError = false; await Processor().ExecuteAsync(await DueAgain(job), default);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(0, await db.Scalar<int>("SELECT FlaggedFields FROM dbo.JobResults"));
        Assert.Single(f.Calls, c => c.StartsWith("post:"));
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
    }
    
    [TestCase(false)]
    [TestCase(true)]
    public async Task Missing_price_does_not_block_completion_and_records_unpriced_usage(bool priced)
    {
        f.CompletedAtUtc = new DateTimeOffset(priced ? 2026 : 2025, 10, 15, 12, 0, 0, TimeSpan.FromHours(2));
        var job = await db.NewClaim(); await Processor().ExecuteAsync(job, default);
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
        Assert.Equal(priced ? 0 : 1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE PricePer1000 IS NULL"));
        Assert.Equal(priced ? 1 : 0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE PricePer1000=30"));
        Assert.Equal(f.CompletedAtUtc.Value.UtcDateTime, await db.Scalar<DateTime>("SELECT OccurredAtUtc FROM dbo.UsageLedger"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.completed'"));
    }
    
    [TestCase("AZURE_THROTTLED")] [TestCase("AZURE_UNAVAILABLE")] [TestCase("MAPPING_FAILED")]
    [TestCase("STORAGE_UNAVAILABLE")] [TestCase("INTERNAL_ERROR")] [TestCase("AZURE_AUTH")]
    public async Task Queued_diagnostics_stay_on_Jobs_and_S13_adapter_hides_them(string code)
    {
        var job = await db.NewClaim();
        Assert.Equal(WriteOutcome.Applied, await db.Store.RetryAsync(new(job.JobId, job.LeaseToken), 30, code, default));
        Assert.Equal(code, await db.Scalar<string>("SELECT ErrorCode FROM dbo.Jobs"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE ErrorCode IS NOT NULL"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries"));
        foreach (var runNo in new short?[] { null, job.RunNo })
        {
            var run = Assert.IsType<RunOutcome>(await db.Store.GetRunAsync(job.JobId, job.ConsumerId, runNo, default));
            Assert.Equal("Queued", run.Status); Assert.Null(run.ErrorCode); Assert.Null(run.ErrorMessage);
        }
        await db.Execute("UPDATE dbo.Jobs SET NextAttemptAtUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", job);
        var resumed = Assert.IsType<ClaimedJob>(await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        var active = Assert.IsType<RunOutcome>(await db.Store.GetRunAsync(job.JobId, job.ConsumerId, job.RunNo, default));
        Assert.Equal("Processing", active.Status); Assert.Null(active.ErrorCode); Assert.Null(active.ErrorMessage);
        await db.Store.FailAsync(new(job.JobId, resumed.LeaseToken, "RETRIES_EXHAUSTED", "Retry budget exhausted.", true, Guid.NewGuid(), "{}"), default);
        Assert.Equal("RETRIES_EXHAUSTED", (await db.Store.GetRunAsync(job.JobId, job.ConsumerId, job.RunNo, default))!.ErrorCode);
    }
    [Test]
    public async Task Old_lease_changes_nothing_and_never_POSTs()
    {
        var job = await db.NewClaim(); await TakeOver(job); var before = await db.Snapshot();
        await Processor().ExecuteAsync(job, default);
        Assert.Equal(before, await db.Snapshot()); Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    [Test]
    public async Task Three_takeovers_fail_without_using_an_attempt_or_POSTing()
    {
        var job = await db.NewClaim(); for (var i = 0; i < 3; i++) job = await TakeOver(job);
        await Processor().ExecuteAsync(job, default);
        Assert.Equal("WORKER_CRASH_LOOP", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.ErrorCode);
        Assert.Equal(0, await db.Scalar<int>("SELECT Attempts FROM dbo.Jobs")); Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    [Test]
    public async Task Poll_404_atomically_fails_call_job_run_and_event()
    {
        var job = await db.NewClaim(); await db.Accepted(job); f.Polls.Enqueue(new PollResult.NotFound());
        await Processor().ExecuteAsync(job, default);
        Assert.Equal("Failed", await db.Scalar<string>("SELECT State FROM dbo.AzureOperations"));
        Assert.Equal("Failed", await db.Scalar<string>("SELECT Status FROM dbo.Jobs"));
        Assert.Equal("ANALYSIS_RESULT_MISSING", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.ErrorCode);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.failed'"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
}
