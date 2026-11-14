using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class AutoManifestSqlTests 
{
    private readonly SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();

    private Task SeedRouter() => db.Execute("""
        INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,TestFieldAccuracy) VALUES
            ('manifest-v1','MANIFEST','extract',1,'Testing',.99),('router','AUTO','classify',1,'Testing',.99);
        INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode) VALUES
            ('router','bill-of-lading','BL',NULL),('router','cargo-manifest','MANIFEST',NULL),('router','other',NULL,NULL);
        """);

    [Test, Category("Sql")]
    public async Task Activation_accepts_AUTO_targets_and_rejection_then_catalog_exposes_only_active_classifier()
    {
        SqlTestGuard.Require();
        await SeedRouter();
        Assert.DoesNotContain(await db.Store.GetCatalogAsync(default), m => m.DocumentTypeCode == "AUTO");
        await db.Store.ActivateAsync("router", .95m, default);
        Assert.Contains(await db.Store.GetCatalogAsync(default), m => m.DocumentTypeCode == "AUTO" && m.Kind == "classify");
        Assert.DoesNotContain(await db.Store.GetCatalogAsync(default), m => m.ModelId == "manifest-v1");
        var accepted = (await db.Store.GetClassMappingAsync("router", "cargo-manifest", default))!;
        Assert.Equal("MANIFEST", accepted.TargetDocumentTypeCode); Assert.Null(accepted.ShippingLineCode);
        var rejected = (await db.Store.GetClassMappingAsync("router", "other", default))!;
        Assert.Null(rejected.TargetDocumentTypeCode); Assert.Null(rejected.ShippingLineCode);
    }

    
    [TestCase("no-routable")]
    [TestCase("unsupported")]
    [TestCase("wrong-line")]
    [TestCase("explicit-mismatch")]
    public async Task Activation_rejects_invalid_class_targets_without_retiring_models(string mutation)
    {
        SqlTestGuard.Require();
        await SeedRouter();
        await db.Execute(mutation switch
        {
            "no-routable" => "UPDATE dbo.ClassifierClasses SET TargetDocumentTypeCode=NULL WHERE ClassifierModelId='router'",
            "unsupported" => "UPDATE dbo.ClassifierClasses SET TargetDocumentTypeCode='UNKNOWN' WHERE ClassifierModelId='router' AND ClassName='cargo-manifest'",
            "wrong-line" => "UPDATE dbo.ClassifierClasses SET ShippingLineCode='WRONG' WHERE ClassifierModelId='router' AND ClassName='cargo-manifest'",
            _ => "UPDATE dbo.ModelRegistry SET DocumentTypeCode='BL' WHERE ModelId='router'"
        });
        var before = await db.Scalar<string>("SELECT * FROM dbo.ModelRegistry ORDER BY ModelId FOR JSON PATH,INCLUDE_NULL_VALUES");
        Assert.Equal(50003, (await Assert.ThrowsAsync<SqlException>(() => db.Store.ActivateAsync("router", .95m, default))).Number);
        Assert.Equal(before, await db.Scalar<string>("SELECT * FROM dbo.ModelRegistry ORDER BY ModelId FOR JSON PATH,INCLUDE_NULL_VALUES"));
    }

    [Test, Category("Sql")]
    public async Task Null_line_route_is_durable_fenced_billed_once_and_older_run_keeps_own_type()
    {
        SqlTestGuard.Require();
        await SeedRouter();
        var request = SqlDatabase.Request("AUTO");
        await db.Store.IntakeAsync(request, default);
        var claim = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        Assert.Equal("AUTO", claim.DocumentTypeCode); Assert.Null(claim.ResolvedDocumentTypeCode); Assert.Equal("Classify", claim.Stage);
        var op = await db.Accepted(claim, "classify", "router");
        var decision = new ClassifierSuccess(claim.JobId, claim.LeaseToken, op, "router", 2, DateTimeOffset.UtcNow,
            "cargo-manifest", .99m, null, null, Guid.NewGuid(), "{}", "MANIFEST");
        var before = await db.Snapshot();
        Assert.Equal(WriteOutcome.LeaseLost, await db.Store.ClassifierSucceededAsync(decision with { LeaseToken = Guid.NewGuid() }, default));
        Assert.Equal(before, await db.Snapshot());
        Assert.Equal(WriteOutcome.Applied, await db.Store.ClassifierSucceededAsync(decision, default));
        var position = (await db.Store.GetPositionAsync(claim.JobId, default))!;
        Assert.Equal("AUTO", position.RequestedDocumentTypeCode); Assert.Equal("MANIFEST", position.ResolvedDocumentTypeCode);
        Assert.Equal("Extract", position.Stage); Assert.Null(position.ShippingLineCode);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE ResolvedDocumentTypeCode='MANIFEST'"));
        var classified = await db.Snapshot();
        Assert.Equal(WriteOutcome.LeaseLost, await db.Store.ClassifierSucceededAsync(decision, default));
        Assert.Equal(classified, await db.Snapshot());
        var extraction = await db.Accepted(claim, "extract", "manifest-v1");
        await db.Store.ExtractionSucceededAsync(new(claim.JobId, claim.LeaseToken, extraction, "manifest-v1", 2, DateTimeOffset.UtcNow, "raw.json"), default);
        await db.Store.CompleteAsync(new(claim.JobId, claim.LeaseToken, "{\"documentTypeCode\":\"MANIFEST\"}", 0,
            Guid.NewGuid(), "{}", "cargo-manifest.v1"), default);
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_CLASSIFY'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_EXTRACT'"));
        await db.Store.IntakeAsync(request with { ClientRequestId = "run2", Reprocess = true }, default);
        position = (await db.Store.GetPositionAsync(claim.JobId, default))!;
        Assert.Equal((short)2, position.RunNo); Assert.Equal("Classify", position.Stage); Assert.Null(position.ResolvedDocumentTypeCode);
        var next = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        var secondOp = await db.Accepted(next, "classify", "router");
        await db.Store.ClassifierSucceededAsync(decision with { LeaseToken = next.LeaseToken, OperationId = secondOp,
            ClassName = "bill-of-lading", ResolvedDocumentTypeCode = "BL", EventId = Guid.NewGuid() }, default);
        var old = (await db.Store.GetRunAsync(claim.JobId, 1, 1, default))!;
        Assert.Equal("AUTO", old.RequestedDocumentTypeCode); Assert.Equal("MANIFEST", old.ResolvedDocumentTypeCode);
        Assert.Equal("cargo-manifest.v1", old.ResultType); Assert.Equal("Completed", old.Status);
        var current = (await db.Store.GetRunAsync(claim.JobId, 1, null, default))!;
        Assert.Equal("BL", current.ResolvedDocumentTypeCode); Assert.Null(current.ResultType);
    }

    [Test, Category("Sql")]
    public async Task AUTO_and_explicit_BL_keep_separate_identity_and_repeated_AUTO_reuses_its_job()
    {
        SqlTestGuard.Require();
        var request = SqlDatabase.Request("BL");
        var bl = await db.Store.IntakeAsync(request, default);
        var autoRequest = request with { JobId = Guid.NewGuid(), ClientRequestId = "auto", DocumentTypeCode = "AUTO" };
        var auto = await db.Store.IntakeAsync(autoRequest, default);
        Assert.NotEqual(bl.JobId, auto.JobId);
        var repeated = await db.Store.IntakeAsync(autoRequest with { JobId = Guid.NewGuid(), ClientRequestId = "again" }, default);
        Assert.Equal(auto.JobId, repeated.JobId); Assert.Equal(IntakeOutcome.Existing, repeated.Outcome);
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE DocumentTypeCode='BL' AND ResolvedDocumentTypeCode='BL' AND Stage='Extract'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE DocumentTypeCode='AUTO' AND ResolvedDocumentTypeCode IS NULL AND Stage='Classify'"));
    }

    [Test, Category("Sql")]
    public async Task Rejected_AUTO_class_commits_only_classification_usage_and_failed_outcome()
    {
        SqlTestGuard.Require();
        await SeedRouter();
        var job = await db.NewClaim("AUTO"); var op = await db.Accepted(job, "classify", "router");
        Assert.Equal(WriteOutcome.Applied, await db.Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, op,
            "router", 2, DateTimeOffset.UtcNow, "other", .99m, null, "unsupported", Guid.NewGuid(), "{}"), default));
        var run = (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!;
        Assert.Equal("Failed", run.Status); Assert.Equal("UNCLASSIFIED", run.ErrorCode);
        Assert.Null(run.ResolvedDocumentTypeCode); Assert.Null(run.ResultType);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_CLASSIFY'"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE Kind='extract'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE EventType='job.failed'"));
    }

    [Test, Category("Sql")]
    public async Task Explicit_type_cannot_be_reclassified_to_another_type()
    {
        SqlTestGuard.Require();
        var claim = await db.NewClaim("INV"); var op = await db.Accepted(claim, "classify", "inv-classifier");
        var before = await db.Snapshot();
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Store.ClassifierSucceededAsync(new(claim.JobId, claim.LeaseToken,
            op, "inv-classifier", 2, DateTimeOffset.UtcNow, "class", .99m, null, null, Guid.NewGuid(), "{}", "MANIFEST"), default));
        Assert.Equal(50014, error.Number); Assert.Equal(before, await db.Snapshot());
    }
}
