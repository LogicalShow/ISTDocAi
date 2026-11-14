using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed partial class PageQuotaSqlTests 
{
    private SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private Task<long> Balance() => db.Scalar<long>("SELECT AvailablePages FROM dbo.ConsumerPageBalance WHERE ConsumerId=1");
    private Task Grant(long pages, Guid? requestId = null) => db.Execute("""
        INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId)
        VALUES(1,@pages,'TopUp','test-operator','package',@requestId);
        """, new { pages, requestId = requestId ?? Guid.NewGuid() });
    private Task Prepaid() => db.Execute("UPDATE dbo.Consumers SET QuotaMode='Prepaid' WHERE ConsumerId=1");
    private async Task<ClaimedJob> Claim(IntakeRequest request)
    {
        await db.Store.IntakeAsync(request, default);
        return (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
    }
    private Task<WriteOutcome> Fail(ClaimedJob job) => db.Store.FailAsync(new(job.JobId, job.LeaseToken,
        "FAILED", null, false, Guid.NewGuid(), "{}"), default);
    private async Task Extract(ClaimedJob job, string model = "bl-v1")
    {
        var op = await db.Accepted(job, "extract", model);
        Assert.Equal(WriteOutcome.Applied, await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken,
            op, model, 1, DateTimeOffset.UtcNow, "raw"), default));
    }
    private Task<WriteOutcome> Complete(ClaimedJob job) => db.Store.CompleteAsync(new(job.JobId, job.LeaseToken,
        "{}", 0, Guid.NewGuid(), "{}", MappingFingerprint: InMemoryMappingProfileResolver.Fingerprint(new(), "bl-v1", "BL")), default);
    private async Task Classify(ClaimedJob job, bool rejected = false)
    {
        var op = await db.Accepted(job, "classify", "inv-classifier");
        Assert.Equal(WriteOutcome.Applied, await db.Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken,
            op, "inv-classifier", 1, DateTimeOffset.UtcNow, "line-class", .99m, rejected ? null : "LINE",
            null, Guid.NewGuid(), "{}", rejected ? null : "INV"), default));
    }

    [Test, Category("Sql")]
    public async Task Concurrent_six_page_intakes_with_ten_pages_allow_exactly_one()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(10);
        async Task<object> Submit()
        {
            try { return await db.Store.IntakeAsync(SqlDatabase.Request() with { PageCount=6 }, default); }
            catch (IntakeException e) { return e; }
        }
        var outcomes = await Task.WhenAll(Submit(), Submit());
        Assert.Single(outcomes.OfType<IntakeResult>(), x => x.Outcome==IntakeOutcome.Created);
        var error=Assert.Single(outcomes.OfType<IntakeException>());
        Assert.Equal(402,error.Status); Assert.Equal("QUOTA_EXHAUSTED",error.Code);
        Assert.Equal(6,error.RequiredPages); Assert.Equal(4,error.AvailablePages);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations"));
        Assert.Equal(4,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Duplicate_race_has_one_run_and_existing_return_works_at_zero()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(2);
        var request=SqlDatabase.Request();
        var results=await Task.WhenAll(db.Store.IntakeAsync(request,default),db.Store.IntakeAsync(request,default));
        Assert.Single(results,x=>x.Outcome==IntakeOutcome.Created);
        Assert.Single(results,x=>x.Outcome==IntakeOutcome.Existing);
        Assert.Equal(0,await Balance());
        Assert.Equal(IntakeOutcome.Existing,(await db.Store.IntakeAsync(request with { ClientRequestId="alias" },default)).Outcome);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations"));
    }

    [Test, Category("Sql")]
    public async Task ModelChanged_at_zero_returns_old_result_but_explicit_reprocess_is_402()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(2);
        var request=SqlDatabase.Request(); var job=await Claim(request);
        await Extract(job); await Complete(job);
        await db.Execute("UPDATE dbo.JobRuns SET MappingFingerprint=NULL");
        var result=await db.Store.IntakeAsync(request with { ClientRequestId="refresh",BlobPath=null },default);
        Assert.Equal(IntakeOutcome.Existing,result.Outcome); Assert.Equal((short)1,result.RunNo);
        Assert.Equal("QUOTA_EXHAUSTED",result.RereadSkipped);
        Assert.Equal("Completed",result.RunStatus);
        var error=await Assert.ThrowsAsync<IntakeException>(()=>db.Store.IntakeAsync(request with { ClientRequestId="explicit",Reprocess=true },default));
        Assert.Equal(402,error.Status);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
        Assert.Equal(2,await db.Scalar<long>("SELECT Pages FROM dbo.CustomerPageCharges"));
    }

    
    [TestCase(false)] [TestCase(true)]
    public async Task Billed_classify_then_extraction_failure_or_rejection_still_charges_once(bool rejected)
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(10);
        var job=await Claim(SqlDatabase.Request("INV") with { PageCount=6 });
        await Classify(job,rejected);
        if(!rejected) await Fail(job);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
        Assert.Equal(6,await db.Scalar<long>("SELECT Pages FROM dbo.CustomerPageCharges"));
        Assert.Equal("Charged",await db.Scalar<string>("SELECT State FROM dbo.PageReservations"));
        Assert.Equal(4,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Unbilled_failure_releases_and_failed_requeue_at_insufficient_balance_is_refused()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(2);
        var request=SqlDatabase.Request(); var job=await Claim(request);
        await Fail(job); Assert.Equal(2,await Balance());
        Assert.Equal("Released",await db.Scalar<string>("SELECT State FROM dbo.PageReservations"));
        await db.Store.IntakeAsync(SqlDatabase.Request(),default);
        var error=await Assert.ThrowsAsync<IntakeException>(()=>db.Store.IntakeAsync(request with { ClientRequestId="requeue" },default));
        Assert.Equal(402,error.Status);
        Assert.Equal(0,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
    }

    [Test, Category("Sql")]
    public async Task Two_meters_and_replays_charge_document_pages_once()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(10);
        var job=await Claim(SqlDatabase.Request("INV") with { PageCount=6 });
        await Classify(job); await Extract(job,"inv-line");
        var complete=new Completion(job.JobId,job.LeaseToken,"{}",0,Guid.NewGuid(),"{}");
        Assert.Equal(WriteOutcome.Applied,await db.Store.CompleteAsync(complete,default));
        Assert.Equal(WriteOutcome.LeaseLost,await db.Store.CompleteAsync(complete,default));
        await db.Execute("EXEC dbo.ReconcilePageReservations; EXEC dbo.ReconcilePageReservations;");
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
        Assert.Equal(6,await db.Scalar<long>("SELECT Pages FROM dbo.CustomerPageCharges"));
        Assert.Equal(4,await Balance());
        await db.Execute("""
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,ReversesUsageId,CorrelationId)
            SELECT TOP(1) ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,-Pages,PricePer1000,OccurredAtUtc,UsageId,CorrelationId
            FROM dbo.UsageLedger WHERE ReversesUsageId IS NULL ORDER BY UsageId;
            EXEC dbo.ReconcilePageReservations;
            """);
        Assert.Equal(4,await Balance());
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
    }

    [Test, Category("Sql")]
    public async Task Retry_and_takeover_keep_active_reservation_even_during_maintenance()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(10);
        var job=await Claim(SqlDatabase.Request());
        await db.Store.RetryAsync(new(job.JobId,job.LeaseToken),30,"RETRY",default);
        await db.Execute("UPDATE dbo.Jobs SET NextAttemptAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()), LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME())");
        var next=(await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME())");
        var takeover=(await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        Assert.NotEqual(next.LeaseToken,takeover.LeaseToken);
        await db.Store.RunAsync(default);
        Assert.Equal("Reserved",await db.Scalar<string>("SELECT State FROM dbo.PageReservations"));
        Assert.Equal(8,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Reconciliation_charges_billed_orphan_releases_unbilled_terminal_and_keeps_active()
    {
        SqlTestGuard.Require();
        var billed=await Claim(SqlDatabase.Request()); var op=await db.Accepted(billed);
        // Emulate a pre-cutover billed active run; bypass application settlement deliberately.
        await db.Execute("""
            UPDATE dbo.AzureOperations SET State='Succeeded',Pages=1,CompletedAtUtc=SYSUTCDATETIME() WHERE OperationId=@op;
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,OccurredAtUtc,CorrelationId)
            VALUES(1,@JobId,1,@op,'DI_CUSTOM_EXTRACT','bl-v1',1,SYSUTCDATETIME(),'test');
            UPDATE dbo.JobRuns SET Status='Failed' WHERE JobId=@JobId;
            UPDATE dbo.Jobs SET Status='Failed',LeaseToken=NULL,LeaseExpiresAtUtc=NULL WHERE JobId=@JobId;
            """,new { billed.JobId,op });
        var unbilled=await db.Store.IntakeAsync(SqlDatabase.Request(),default);
        await db.Execute("UPDATE dbo.JobRuns SET Status='Failed' WHERE JobId=@JobId; UPDATE dbo.Jobs SET Status='Failed' WHERE JobId=@JobId",new { unbilled.JobId });
        await db.Store.IntakeAsync(SqlDatabase.Request(),default);
        await db.Store.RunAsync(default); await db.Store.RunAsync(default);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations WHERE State='Charged'"));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations WHERE State='Released'"));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations WHERE State='Reserved'"));
        Assert.Equal(-4,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Completion_without_billed_usage_explicitly_releases()
    {
        SqlTestGuard.Require();
        var job=await Claim(SqlDatabase.Request());
        await db.Execute("UPDATE dbo.Jobs SET Stage='Map'");
        await Complete(job);
        Assert.Equal("Released",await db.Scalar<string>("SELECT State FROM dbo.PageReservations"));
        Assert.Equal(0,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Unlimited_allows_negative_balance_but_records_reservation_and_charge()
    {
        SqlTestGuard.Require();
        var job=await Claim(SqlDatabase.Request());
        Assert.Equal(-2,await Balance());
        await Extract(job); await Complete(job);
        Assert.Equal(-2,await Balance());
        Assert.Equal("Charged",await db.Scalar<string>("SELECT State FROM dbo.PageReservations"));
    }

    [Test, Category("Sql")]
    public async Task Topups_add_to_remaining_capture_cycle_base_and_replay_idempotently()
    {
        SqlTestGuard.Require();
        var id=Guid.NewGuid(); await Grant(100,id); await Grant(100,id);
        await db.Store.IntakeAsync(SqlDatabase.Request() with { PageCount=80 },default);
        await Grant(100);
        Assert.Equal(120,await Balance());
        Assert.Equal(120,await db.Scalar<long>("SELECT TOP(1) BalanceAfter FROM dbo.PageAllowanceGrants ORDER BY GrantId DESC"));
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageAllowanceGrants"));
    }

    [Test, Category("Sql")]
    public async Task Topup_holds_consumer_lock_until_commit_and_racing_intake_sees_new_balance()
    {
        SqlTestGuard.Require();
        await Prepaid();
        await using var connection=new SqlConnection(db.ConnectionString); await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId) VALUES(1,10,'TopUp','operator','package',NEWID())",transaction:tx);
        var intake=db.Store.IntakeAsync(SqlDatabase.Request() with { PageCount=6 },default);
        await tx.CommitAsync();
        Assert.Equal(IntakeOutcome.Created,(await intake).Outcome);
        Assert.Equal(4,await Balance());
        Assert.Equal(10,await db.Scalar<long>("SELECT BalanceAfter FROM dbo.PageAllowanceGrants"));
    }

    [Test, Category("Sql")]
    public async Task Atomic_daily_cap_serializes_two_uploads_and_preserves_429()
    {
        SqlTestGuard.Require();
        await db.Execute("UPDATE dbo.Consumers SET DailyPageCap=10");
        async Task<object> Intake()
        {
            try { return await db.Store.IntakeAsync(SqlDatabase.Request() with { PageCount=6 },default); }
            catch(IntakeException e) { return e; }
        }
        var results=await Task.WhenAll(Intake(),Intake());
        Assert.Single(results.OfType<IntakeResult>());
        var error=Assert.Single(results.OfType<IntakeException>());
        Assert.Equal(429,error.Status); Assert.Equal("DAILY_CAP_REACHED",error.Code);
        Assert.Equal(6,await db.Store.DailyPagesAsync(1,default));
    }

    [Test, Category("Sql")]
    public async Task Runtime_cannot_write_allowances_and_audit_rows_cannot_be_edited()
    {
        SqlTestGuard.Require();
        await db.Execute("IF DATABASE_PRINCIPAL_ID(N'docai_runtime') IS NULL CREATE ROLE docai_runtime; CREATE USER quota_runtime WITHOUT LOGIN; ALTER ROLE docai_runtime ADD MEMBER quota_runtime;");
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"06_page_quota_upgrade.sql")));
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.Execute("""
            EXECUTE AS USER='quota_runtime';
            BEGIN TRY
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId) VALUES(1,10,'TopUp','runtime','invalid',NEWID());
            REVERT;
            END TRY BEGIN CATCH REVERT; THROW; END CATCH;
            """));
        Assert.Equal(229,error.Number);
        await Grant(10);
        Assert.Equal(50017,(await Assert.ThrowsAsync<SqlException>(()=>db.Execute("UPDATE dbo.PageAllowanceGrants SET Pages=99"))).Number);
        Assert.Equal(50017,(await Assert.ThrowsAsync<SqlException>(()=>db.Execute("DELETE dbo.PageAllowanceGrants"))).Number);
        Assert.Equal(10,await Balance());
    }

    [Test, Category("Sql")]
    public async Task Invalid_grants_and_cross_consumer_links_are_rejected_and_adjustments_are_signed()
    {
        SqlTestGuard.Require();
        await Grant(10);
        await db.Execute("INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1) VALUES('SECOND','Second','FWD','SECOND',CONVERT(binary(32),2))");
        foreach(var insert in new[] {
            "VALUES(1,0,'Adjustment',NULL,'operator','bad',NEWID())",
            "VALUES(2,-10,'Reversal',1,'operator','cross consumer',NEWID())" })
            await Assert.ThrowsAsync<SqlException>(()=>db.Execute("INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId) "+insert));
        await db.Execute("INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId) VALUES(1,-3,'Adjustment',1,'operator','correction',NEWID())");
        Assert.Equal(7,await Balance());
        Assert.Equal(7,await db.Scalar<long>("SELECT TOP(1) BalanceAfter FROM dbo.PageAllowanceGrants ORDER BY GrantId DESC"));
    }
    [Test, Category("Sql")]
    public async Task Upgrade_reserves_active_runs_only_and_is_rerunnable()
    {
        SqlTestGuard.Require();
        var legacy=new SqlDatabase { UseLegacySchema=true };
        try
        {
            await legacy.InitializeAsync();
            foreach(var file in new[] { "03_auto_manifest_upgrade.sql", "04_consumer_page_limit_upgrade.sql", "05_run_mapping_fingerprint_upgrade.sql" })
                await legacy.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,file)));
            await legacy.Execute("""
                DECLARE @active uniqueidentifier=NEWID(),@old uniqueidentifier=NEWID();
                INSERT dbo.Jobs(JobId,ConsumerId,DocumentTypeCode,IdemLine,FileSha256,ContentType,FileSizeBytes,PageCount,BlobPath,Stage,CorrelationId,ResolvedDocumentTypeCode)
                VALUES(@active,1,'BL','*',CONVERT(binary(32),11),'application/pdf',100,6,'file','Extract','test','BL'),
                      (@old,1,'BL','*',CONVERT(binary(32),12),'application/pdf',100,2,'file','Extract','test','BL');
                INSERT dbo.JobRuns(JobId,RunNo,Reason,ConsumerId,PageCount,CorrelationId,Status)
                VALUES(@active,1,'New',1,6,'test','Active'),(@old,1,'New',1,2,'test','Completed');
                UPDATE dbo.Jobs SET Status='Completed' WHERE JobId=@old;
                """);
            var upgrade=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"06_page_quota_upgrade.sql"));
            await legacy.Execute(upgrade); await legacy.Execute(upgrade);
            Assert.Equal(1,await legacy.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations"));
            Assert.Equal(6,await legacy.Scalar<long>("SELECT Pages FROM dbo.PageReservations"));
            Assert.Equal("Unlimited",await legacy.Scalar<string>("SELECT QuotaMode FROM dbo.Consumers"));
            Assert.Equal(10,await legacy.Scalar<int>("SELECT QuotaWarningPercent FROM dbo.Consumers"));
            Assert.Equal(0,await legacy.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"));
            await legacy.Execute("UPDATE dbo.JobRuns SET Status='Failed' WHERE Status='Active'; EXEC dbo.ReconcilePageReservations;");
            Assert.Equal("Released",await legacy.Scalar<string>("SELECT State FROM dbo.PageReservations"));
        }
        finally { await legacy.DisposeAsync(); }
    }

}
