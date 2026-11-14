using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Collections.Concurrent;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class SqlIntegrationTests 
{
    private readonly SqlDatabase db = new();
    private SqlDocAiStore Store => db.Store;
    [SetUp]
    public Task InitializeAsync() => db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();

    [Test]
    public async Task Intake_upload_binding_and_failed_requeue_preserve_old_run()
    {
        SqlTestGuard.Require();
        var request = SqlDatabase.Request();
        Assert.Equal(IntakeOutcome.NeedsUpload, (await Store.IntakeAsync(request with { BlobPath = null }, default)).Outcome);
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs"));
        var created = await Store.IntakeAsync(request, default);
        Assert.Equal(IntakeOutcome.Created, created.Outcome);
        Assert.Equal((short)1, created.RunNo);
        Assert.Equal("Queued", created.RunStatus);
        Assert.Equal(IntakeOutcome.Existing, (await Store.IntakeAsync(request, default)).Outcome);
        var another = request with { ClientRequestId = "another-record", BlobPath = null };
        Assert.Equal(IntakeOutcome.Existing, (await Store.IntakeAsync(another, default)).Outcome);
        var job = Assert.IsType<ClaimedJob>(await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.Equal("Extract", job.Stage);
        Assert.Equal(2, await Store.DailyPagesAsync(1, default));
        Assert.Equal(WriteOutcome.Applied, await Store.FailAsync(new(job.JobId, job.LeaseToken, "FAILED", "failure", false, Guid.NewGuid(), "{}"), default));
        var resend = request with { ClientRequestId = "new-submission" };
        Assert.Equal(IntakeOutcome.NeedsUpload, (await Store.IntakeAsync(resend with { BlobPath = null }, default)).Outcome);
        var requeued = await Store.IntakeAsync(resend, default);
        Assert.Equal(IntakeOutcome.Requeued, requeued.Outcome);
        Assert.Equal((short)2, requeued.RunNo);
        Assert.Equal("Superseded", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        var bound = await Store.IntakeAsync(another, default);
        Assert.Equal((short)1, bound.RunNo);
        Assert.Equal("Failed", bound.RunStatus);
        Assert.Equal("Failed", (await Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
        Assert.Equal("Queued", (await Store.GetRunAsync(job.JobId, 1, null, default))!.Status);
        Assert.Null(await Store.GetRunAsync(job.JobId, 2, null, default));
        Assert.Equal(4, await Store.DailyPagesAsync(1, default));
    }

    [Test]
    public async Task Classifier_only_type_starts_and_requeues_at_Classify_despite_other_types_generic_model()
    {
        SqlTestGuard.Require();
        var request = SqlDatabase.Request("INV");
        await Store.IntakeAsync(request, default);
        var claim = Assert.IsType<ClaimedJob>(await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.Equal("Classify", claim.Stage);
        await Store.FailAsync(new(claim.JobId, claim.LeaseToken, "FAILED", null, false, Guid.NewGuid(), "{}"), default);
        await Store.IntakeAsync(request with { ClientRequestId = "retry-inv" }, default);
        Assert.Equal("Classify", (await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync)).Stage);
    }

    [Test]
    public async Task Two_parallel_claimers_claim_fifty_jobs_exactly_once_under_RCSI_and_take_over_expired_lease()
    {
        SqlTestGuard.Require();
        Assert.Equal(1, await db.Scalar<int>("SELECT CONVERT(int,is_read_committed_snapshot_on) FROM sys.databases WHERE name=DB_NAME()"));
        for (var i = 0; i < 50; i++) await Store.IntakeAsync(SqlDatabase.Request(), default);
        var claims = new ConcurrentBag<ClaimedJob>();
        async Task Drain()
        {
            while (await Store.ClaimAsync(default) is { } job) claims.Add(job);
        }
        await Task.WhenAll(Drain(), Drain());
        Assert.Equal(50, claims.Count);
        Assert.Equal(50, claims.Select(j => j.JobId).Distinct().Count());
        var expired = claims.First();
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", expired);
        var takeover = Assert.IsType<ClaimedJob>(await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.Equal(expired.JobId, takeover.JobId);
        Assert.NotEqual(expired.LeaseToken, takeover.LeaseToken);
        Assert.Equal(1, takeover.TakeoverCount);
        Assert.Equal(WriteOutcome.LeaseLost, await Store.RenewAsync(new(expired.JobId, expired.LeaseToken), default));
    }

    
    [TestCase("renew")]
    [TestCase("retry")]
    [TestCase("release")]
    [TestCase("begin")]
    [TestCase("close")]
    [TestCase("extract")]
    [TestCase("complete")]
    [TestCase("fail")]
    [TestCase("classify")]
    public async Task Every_guarded_worker_write_with_stale_token_changes_nothing(string action)
    {
        SqlTestGuard.Require();
        var classify = action == "classify";
        var job = await db.NewClaim(classify ? "INV" : "BL");
        var begin = await Store.BeginAsync(new(job.JobId, job.LeaseToken), classify ? "classify" : "extract", classify ? "inv-classifier" : "bl-v1", default);
        var operation = begin.OperationId!.Value;
        if (action is "extract" or "classify" or "fail" or "complete") await Store.AcceptedAsync(operation, "https://azure.example/op", default);
        if (action == "complete") await Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2, DateTimeOffset.UtcNow, "raw.json"), default);
        // An actual takeover replaces the original token, rather than simply passing a random token.
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", job);
        var next = Assert.IsType<ClaimedJob>(await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.NotEqual(job.LeaseToken, next.LeaseToken);
        var before = await db.Snapshot();
        var stale = new Lease(job.JobId, job.LeaseToken);
        var outcome = action switch
        {
            "renew" => await Store.RenewAsync(stale, default),
            "retry" => await Store.RetryAsync(stale, 30, "TIMEOUT", default),
            "release" => await Store.ReleaseAsync(stale, 0, null, default),
            "begin" => (await Store.BeginAsync(stale, "classify", "inv-classifier", default)).Outcome,
            "close" => await Store.CloseSendingAsync(operation, stale.LeaseToken, "Unknown", null, default),
            "extract" => await Store.ExtractionSucceededAsync(new(job.JobId, stale.LeaseToken, operation, "bl-v1", 2, DateTimeOffset.UtcNow, "raw.json"), default),
            "complete" => await Store.CompleteAsync(new(job.JobId, stale.LeaseToken, "{}", 0, Guid.NewGuid(), "{}"), default),
            "fail" => await Store.FailAsync(new(job.JobId, stale.LeaseToken, "FAILED", "error", true, Guid.NewGuid(), "{}", operation, "AzureFailure"), default),
            _ => await Store.ClassifierSucceededAsync(new(job.JobId, stale.LeaseToken, operation, "inv-classifier", 2, DateTimeOffset.UtcNow, "line-class", .9m, "LINE", null, Guid.NewGuid(), "{}", "INV"), default)
        };
        Assert.Equal(WriteOutcome.LeaseLost, outcome);
        Assert.Equal(before, await db.Snapshot());
    }

    
    [TestCase(true)]
    [TestCase(false)]
    public async Task Extraction_records_completion_date_price_before_mapping_then_completes_everything(bool priced)
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        var operation = await db.Accepted(job);
        var completed = new DateTimeOffset(priced ? 2026 : 2025, 10, 15, 12, 0, 0, TimeSpan.FromHours(2));
        if (priced) await db.Execute("INSERT dbo.AzureMeterPrices VALUES('DI_CUSTOM_EXTRACT','2026-10-20',99,'USD','future price')");
        Assert.Equal(WriteOutcome.Applied, await Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2, completed, "results/raw.json"), default));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(priced ? 1 : 0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE PricePer1000=30"));
        Assert.Equal(priced ? 0 : 1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE PricePer1000 IS NULL"));
        Assert.Equal(completed.UtcDateTime, await db.Scalar<DateTime>("SELECT OccurredAtUtc FROM dbo.UsageLedger"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobResults"));
        var position = Assert.IsType<RunPosition>(await Store.GetPositionAsync(job.JobId, default));
        Assert.Equal("Map", position.Stage);
        Assert.Equal(operation, position.MapOperationId);
        Assert.Equal("results/raw.json", position.MapRawResultPath);
        Assert.Equal(WriteOutcome.Applied, await Store.CompleteAsync(new(job.JobId, job.LeaseToken, "{\"result\":1}", 1, Guid.NewGuid(), "{}"), default));
        var result = Assert.IsType<RunOutcome>(await Store.GetRunAsync(job.JobId, 1, 1, default));
        Assert.Equal("Completed", result.Status);
        Assert.Equal(TimeSpan.Zero, result.CreatedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, result.CompletedAtUtc!.Value.Offset);
        Assert.Equal(result.ResultJson, (await Store.GetEventContextAsync(job.JobId, 1, default))?.ResultJson);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE Status='Completed' AND LeaseToken IS NULL"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE Status='Completed'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.completed'"));
    }

    [Test]
    public async Task Two_unknown_calls_refuse_third_and_accepted_call_cannot_be_closed_sending()
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        var lease = new Lease(job.JobId, job.LeaseToken);
        Assert.Equal(0, (await Store.GetPositionAsync(job.JobId, default))!.UnknownInRun);
        for (var i = 0; i < 2; i++)
        {
            var operation = await Store.BeginAsync(lease, "extract", "bl-v1", default);
            Assert.Equal(WriteOutcome.Applied, operation.Outcome);
            Assert.Equal(WriteOutcome.Applied, await Store.CloseSendingAsync(operation.OperationId!.Value, lease.LeaseToken, "Unknown", null, default));
            var position = Assert.IsType<RunPosition>(await Store.GetPositionAsync(job.JobId, default));
            Assert.Equal(i + 1, position.UnknownInRun);
            Assert.Null(position.OperationId);
        }
        var before = await db.Snapshot();
        var refused = await Store.BeginAsync(lease, "extract", "bl-v1", default);
        Assert.Equal(WriteOutcome.RetriesExhausted, refused.Outcome);
        Assert.Null(refused.OperationId);
        Assert.Equal(before, await db.Snapshot());
    }

    [Test]
    public async Task Accepted_fact_survives_takeover_and_close_does_not_change_it()
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        var operation = (await Store.BeginAsync(new(job.JobId, job.LeaseToken), "extract", "bl-v1", default)).OperationId!.Value;
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE JobId=@JobId", job);
        var owner = (await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.True(await Store.AcceptedAsync(operation, "https://azure.example/op", default));
        var before = await db.Snapshot();
        Assert.Equal(WriteOutcome.LeaseLost, await Store.CloseSendingAsync(operation, owner.LeaseToken, "Unknown", null, default));
        Assert.Equal(before, await db.Snapshot());
        Assert.Equal("Accepted", (await Store.GetPositionAsync(job.JobId, default))!.State);
        Assert.Equal(TimeSpan.Zero, (await Store.GetPositionAsync(job.JobId, default))!.AcceptedAtUtc!.Value.Offset);
    }

    [Test]
    public async Task Failure_closes_accepted_operation_job_run_and_event_together()
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        var operation = await db.Accepted(job);
        Assert.Equal(WriteOutcome.Applied, await Store.FailAsync(new(job.JobId, job.LeaseToken, "ANALYSIS_RESULT_MISSING", new string('x', 500), true, Guid.NewGuid(), "{}", operation, "NotFound"), default));
        Assert.Equal("Failed", await db.Scalar<string>("SELECT State FROM dbo.AzureOperations"));
        Assert.Equal("NotFound", await db.Scalar<string>("SELECT AzureErrorCode FROM dbo.AzureOperations"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE Status='Failed' AND Attempts=1 AND LEN(ErrorMessage)=400"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE Status='Failed' AND LEN(ErrorMessage)=400"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.failed'"));
        Assert.Null((await Store.GetEventContextAsync(job.JobId, 1, default))?.ResultJson);
    }

    
    [TestCase("LINE")]
    [TestCase(null)]
    public async Task Classifier_success_commits_usage_and_mapped_or_unclassified_decision(string? line)
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim("INV");
        var operation = await db.Accepted(job, "classify", "inv-classifier");
        var mapping = Assert.IsType<ClassMapping>(await Store.GetClassMappingAsync("inv-classifier", "line-class", default));
        Assert.Equal("LINE", mapping.ShippingLineCode);
        Assert.Equal(.8m, mapping.MinClassConfidence);
        Assert.Null(await Store.GetClassMappingAsync("inv-classifier", "missing", default));
        Assert.Equal(WriteOutcome.Applied, await Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, operation, "inv-classifier", 2, DateTimeOffset.UtcNow, "line-class", .9m, line, "unclassified", Guid.NewGuid(), "{}", line is null ? null : "INV"), default));
        Assert.Equal("Succeeded", await db.Scalar<string>("SELECT State FROM dbo.AzureOperations"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_CLASSIFY'"));
        var result = (await Store.GetRunAsync(job.JobId, 1, null, default))!;
        Assert.Equal(line is null ? "Failed" : "Processing", result.Status);
        Assert.Equal(line is null ? "UNCLASSIFIED" : null, result.ErrorCode);
        Assert.Equal(line is null ? 1 : 0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.failed'"));
        Assert.Equal(line is null ? "Classify" : "Extract", (await Store.GetPositionAsync(job.JobId, default))!.Stage);
    }

    
    [TestCase(false)]
    [TestCase(true)]
    public async Task Closed_operation_returns_explicit_outcome_and_rolls_back_job_update(bool classifier)
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim(classifier ? "INV" : "BL");
        var op = await Store.BeginAsync(new(job.JobId, job.LeaseToken), classifier ? "classify" : "extract", classifier ? "inv-classifier" : "bl-v1", default);
        var before = await db.Snapshot();
        var outcome = classifier
            ? await Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, op.OperationId!.Value, "inv-classifier", 2, DateTimeOffset.UtcNow, "line-class", .9m, "LINE", null, Guid.NewGuid(), "{}", "INV"), default)
            : await Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, op.OperationId!.Value, "bl-v1", 2, DateTimeOffset.UtcNow, "raw.json"), default);
        Assert.Equal(WriteOutcome.OperationNotOpen, outcome);
        Assert.Equal(before, await db.Snapshot());
    }

    [Test]
    public async Task Outbox_wrong_token_changes_nothing_then_delivers_and_replays_only_current_run()
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        await Store.FailAsync(new(job.JobId, job.LeaseToken, "FAIL", null, false, Guid.NewGuid(), "{}"), default);
        var delivery = Assert.Single(await SqlClaimTestHelper.ExpectedDeliveriesAsync(Store.ClaimDueAsync));
        Assert.Empty(await Store.ClaimDueAsync(default));
        var before = await db.Snapshot();
        Assert.Equal(WriteOutcome.LeaseLost, await Store.RecordAttemptAsync(delivery.DeliveryId, Guid.NewGuid(), 200, null, default));
        Assert.Equal(before, await db.Snapshot());
        Assert.Equal(WriteOutcome.Applied, await Store.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, 302, "redirect", default));
        Assert.Equal("Pending", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        await db.Execute("UPDATE dbo.WebhookDeliveries SET Attempts=7,NextAttemptAtUtc=DATEADD(minute,-1,SYSUTCDATETIME())");
        delivery = Assert.Single(await Store.ClaimDueAsync(default));
        await Store.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, null, "timeout", default);
        Assert.Equal("DeadLettered", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Equal(1, await Store.ReplayAsync(1, default));
        delivery = Assert.Single(await SqlClaimTestHelper.ExpectedDeliveriesAsync(Store.ClaimDueAsync));
        await Store.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, 204, null, default);
        Assert.Equal("Delivered", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Equal(WriteOutcome.LeaseLost, await Store.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, 200, null, default));
        await db.Execute("UPDATE dbo.WebhookDeliveries SET Status='DeadLettered'; UPDATE dbo.Jobs SET RunNo=2");
        Assert.Equal(0, await Store.ReplayAsync(1, default));
    }

    
    [TestCase("complete")]
    [TestCase("fail")]
    [TestCase("classify")]
    public async Task Failure_inside_outcome_transaction_rolls_back_all_its_changes(string action)
    {
        SqlTestGuard.Require();
        var classifier = action == "classify";
        var job = await db.NewClaim(classifier ? "INV" : "BL");
        var operation = await db.Accepted(job, classifier ? "classify" : "extract", classifier ? "inv-classifier" : "bl-v1");
        if (action == "complete")
            await Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2, DateTimeOffset.UtcNow, "raw.json"), default);
        var before = await db.Snapshot();
        // Invalid JSON fails S8's result insert or S9/S18's event insert, after the preceding updates.
        var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
        {
            _ = action switch
            {
                "complete" => await Store.CompleteAsync(new(job.JobId, job.LeaseToken, "invalid-json", 0, Guid.NewGuid(), "{}"), default),
                "fail" => await Store.FailAsync(new(job.JobId, job.LeaseToken, "FAIL", null, false, Guid.NewGuid(), "invalid-json", operation), default),
                _ => await Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, operation, "inv-classifier", 2, DateTimeOffset.UtcNow, null, null, null, "unclassified", Guid.NewGuid(), "invalid-json"), default)
            };
        });
        Assert.Equal(547, exception.Number);
        Assert.Equal(before, await db.Snapshot());
    }

    [Test]
    public async Task Same_file_with_different_requested_line_creates_separate_job_and_explicit_reprocess_creates_run()
    {
        SqlTestGuard.Require();
        var request = SqlDatabase.Request();
        var first = await Store.IntakeAsync(request, default);
        var differentLine = await Store.IntakeAsync(request with { JobId = Guid.NewGuid(), ClientRequestId = "line", RequestedLine = "LINE" }, default);
        Assert.Equal(IntakeOutcome.Created, differentLine.Outcome);
        Assert.NotEqual(first.JobId, differentLine.JobId);
        await db.Execute("UPDATE dbo.Jobs SET Status='Completed',CompletedAtUtc=SYSUTCDATETIME() WHERE JobId=@JobId; UPDATE dbo.JobRuns SET Status='Completed' WHERE JobId=@JobId", request);
        var reprocess = request with { ClientRequestId = "reprocess", Reprocess = true };
        Assert.Equal(IntakeOutcome.NeedsUpload, (await Store.IntakeAsync(reprocess with { BlobPath = null }, default)).Outcome);
        Assert.Equal(IntakeOutcome.Requeued, (await Store.IntakeAsync(reprocess, default)).Outcome);
        Assert.Equal("Reprocess", await db.Scalar<string>("SELECT Reason FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=2", request));
    }

    [Test]
    public async Task Renew_retry_and_release_preserve_accepted_operation_and_attempt_budget()
    {
        SqlTestGuard.Require();
        var job = await db.NewClaim();
        await db.Accepted(job);
        var lease = new Lease(job.JobId, job.LeaseToken);
        Assert.Equal(WriteOutcome.Applied, await Store.RenewAsync(lease, default));
        Assert.Equal(WriteOutcome.Applied, await Store.RetryAsync(lease, 90, "TIMEOUT", default));
        Assert.Null(await Store.ClaimAsync(default));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE Attempts=1 AND Status='Queued' AND LeaseToken IS NULL AND NextAttemptAtUtc>DATEADD(second,80,SYSUTCDATETIME())"));
        await db.Execute("UPDATE dbo.Jobs SET NextAttemptAtUtc=DATEADD(minute,-1,SYSUTCDATETIME())");
        job = (await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync));
        Assert.Equal("Accepted", (await Store.GetPositionAsync(job.JobId, default))!.State);
        Assert.Equal(WriteOutcome.Applied, await Store.ReleaseAsync(new(job.JobId, job.LeaseToken), 600, null, default));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE Attempts=1 AND TakeoverCount=0 AND NextAttemptAtUtc>DATEADD(second,590,SYSUTCDATETIME())"));
    }
    [Test]
    public async Task Activation_retires_only_target_group_and_retention_removes_old_data()
    {
        await db.Execute("INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,TestFieldAccuracy) VALUES('bl-v2','BL','extract',2,'Testing',.99)");
        await db.Execute("INSERT dbo.ModelMappingBindings(ModelId,ProfileId) SELECT 'bl-v2',ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='bl-v2'");
        await Store.ActivateAsync("bl-v2", .95m, default);
        PortAssert.Equal("Retired", await db.Scalar<string>("SELECT Status FROM dbo.ModelRegistry WHERE ModelId='bl-v1'"));
        var catalog = await Store.GetCatalogAsync(default);
        PortAssert.Contains(catalog, m => m.ModelId == "bl-v2");
        PortAssert.Contains(catalog, m => m.ModelId == "inv-classifier");
        PortAssert.Contains(catalog, m => m.ModelId == "inv-line");
        var request = SqlDatabase.Request();
        await Store.IntakeAsync(request, default);
        var job = (await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync))!;
        await Store.FailAsync(new(job.JobId, job.LeaseToken, "FAIL", null, false, Guid.NewGuid(), "{}"), default);
        await db.Execute("""
            UPDATE dbo.Jobs SET CompletedAtUtc=DATEADD(month,-14,SYSUTCDATETIME());
            UPDATE dbo.JobSubmissions SET CreatedAtUtc=DATEADD(month,-14,SYSUTCDATETIME());
            INSERT dbo.JobResults(JobId,RunNo,ContractVersion,ResultJson,FlaggedFields,CreatedAtUtc)
            SELECT JobId,RunNo,'1.0','{}',0,DATEADD(month,-14,SYSUTCDATETIME()) FROM dbo.Jobs;
            UPDATE dbo.WebhookDeliveries SET Status='Superseded',CreatedAtUtc=DATEADD(day,-91,SYSUTCDATETIME());
            """);
        await Store.RunAsync(default);
        PortAssert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE IsDeleted=1 AND FileName IS NULL"));
        PortAssert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobResults"));
        PortAssert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions"));
        PortAssert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries"));
        PortAssert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"));
    }
}
