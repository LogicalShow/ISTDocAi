using ISTDocAi.Application.Services.DocAi.Webhooks;
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
using System.Text.Json;

using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure;
using ProcessingWorkerFakes = ISTDocAi.Infrastructure.Tests.DocAi.Worker.WorkerFakes;
using ISTDocAi.Application.Services.DocAi.Intake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class AutoRoutingWorkerSqlTests
{
    private readonly SqlDatabase db = new();
    private readonly ProcessingWorkerFakes f = new();
    [SetUp]
    public async Task InitializeAsync()
    {
        SqlTestGuard.Require();
        await db.InitializeAsync();
        await db.Execute("""
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES
                ('manifest-v1','MANIFEST','extract',1,'Active'),('doc-router-v1','AUTO','classify',1,'Active');
            INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode) VALUES
                ('doc-router-v1','bill-of-lading','BL',NULL),('doc-router-v1','cargo-manifest','MANIFEST',NULL),('doc-router-v1','other',NULL,NULL);
            """);
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build();
        f.MappingParser = new AnalysisParser(new InMemoryMappingProfileResolver(Options.Create(config.GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!)));
    }
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private sealed class Registry(SqlDocAiStore store) : ICatalogReader, IModelThresholdReader
    {
        public Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct) => store.GetCatalogAsync(ct);
        public Task<decimal?> ReadThresholdAsync(string modelId, CancellationToken ct) => store.GetThresholdAsync(modelId, ct);
    }
    private ProcessJob Processor(IJobRepository? jobs = null) => f.Processor(jobs ?? db.Store, db.Store, db.Store, new Registry(db.Store), new Registry(db.Store));
    private void Responses(string type)
    {
        f.Class = new(type == "BL" ? "bill-of-lading" : "cargo-manifest", .99);
        f.Raw = type == "BL" ? ProcessingWorkerFakes.RawJson : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", "test-A-kestrel-seen.analyze.json"));
        f.Polls.Enqueue(new PollResult.Succeeded("classify", f.Clock.GetUtcNow(), 2));
        f.Polls.Enqueue(new PollResult.Succeeded(f.Raw, f.Clock.GetUtcNow(), 2));
    }

    
    [TestCase("BL", ResultTypes.BillOfLading)] [TestCase("MANIFEST", ResultTypes.Manifest)]
    public async Task AUTO_routes_through_worker_and_persists_typed_result_with_two_usage_rows(string type, string discriminator)
    {
        Responses(type);
        var job = await db.NewClaim("AUTO");
        await Processor().ExecuteAsync(job, default);
        var run = (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!;
        Assert.Equal("Completed", run.Status); Assert.Equal("AUTO", run.RequestedDocumentTypeCode);
        Assert.Equal(type, run.ResolvedDocumentTypeCode); Assert.Equal(discriminator, run.ResultType);
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_CLASSIFY'"));
        var response = await new GetJob(db.Store).ExecuteAsync(job.JobId, new(1, "TEST", "TEST", [], null, 10, 100, true), 1, default);
        Assert.Equal(discriminator, response.ResultType);
        if (type == "MANIFEST") Assert.Equal(9, Assert.IsType<CanonicalManifestResult>(response.Result).BillsOfLading.Sum(b => b.Containers.Length));
    }

    
    [TestCase("other", .99)] [TestCase("unknown", .99)] [TestCase("cargo-manifest", .799)]
    public async Task Rejected_class_commits_one_classification_charge_and_never_posts_extraction(string name, double confidence)
    {
        Responses("MANIFEST"); f.Class = new(name, confidence);
        var job = await db.NewClaim("AUTO"); await Processor().ExecuteAsync(job, default);
        Assert.Equal("UNCLASSIFIED", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.ErrorCode);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE MeterCode='DI_CUSTOM_CLASSIFY'"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE Kind='extract'"));
        Assert.Single(f.Calls, c => c.StartsWith("post:", StringComparison.Ordinal));
    }

    [Test]
    public async Task Crash_after_S18_resumes_extraction_without_reclassification()
    {
        Responses("MANIFEST"); using var stop = new CancellationTokenSource();
        var job = await db.NewClaim("AUTO");
        await Processor(new StopAfterClassification(db.Store, stop)).ExecuteAsync(job, stop.Token);
        Assert.Equal("MANIFEST", (await db.Store.GetPositionAsync(job.JobId, default))!.ResolvedDocumentTypeCode);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        var resumed = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync))!;
        await Processor().ExecuteAsync(resumed, default);
        Assert.Equal("Completed", (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!.Status);
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Single(f.Calls, c => c == "post:doc-router-v1"); Assert.Single(f.Calls, c => c == "post:manifest-v1");
    }



    [Test]
    public async Task Stale_worker_cannot_resolve_or_post_after_the_lease_changes()
    {
        Responses("MANIFEST"); var job = await db.NewClaim("AUTO");
        await db.Execute("UPDATE dbo.Jobs SET LeaseToken=NEWID() WHERE JobId=@JobId", job);
        var before = await db.Snapshot();
        await Processor().ExecuteAsync(job, default);
        Assert.Equal(before, await db.Snapshot()); Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:", StringComparison.Ordinal));
    }

    [Test]
    public async Task Missing_active_extractor_fails_after_classification_without_losing_its_charge()
    {
        Responses("MANIFEST");
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Testing' WHERE ModelId='manifest-v1'");
        var job = await db.NewClaim("AUTO"); await Processor().ExecuteAsync(job, default);
        var run = (await db.Store.GetRunAsync(job.JobId, 1, 1, default))!;
        Assert.Equal("MODEL_NOT_FOUND", run.ErrorCode); Assert.Equal("MANIFEST", run.ResolvedDocumentTypeCode);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE Kind='extract'"));
    }

    private sealed class StopAfterClassification(IJobRepository inner, CancellationTokenSource stop) : IJobRepository
    {
        public Task<int> DailyPagesAsync(short consumerId, CancellationToken ct) => inner.DailyPagesAsync(consumerId, ct);
        public Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken ct) => inner.IntakeAsync(request, ct);
        public Task<RunOutcome?> GetRunAsync(Guid id, short consumer, short? run, CancellationToken ct) => inner.GetRunAsync(id, consumer, run, ct);
        public Task<RunPosition?> GetPositionAsync(Guid id, CancellationToken ct) => inner.GetPositionAsync(id, ct);
        public Task<WriteOutcome> CompleteAsync(Completion request, CancellationToken ct) => inner.CompleteAsync(request, ct);
        public Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken ct) => inner.CancelAsync(consumerId, requestId, ct);
        public Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken ct) => inner.FinishCancellationAsync(lease, ct);
        public Task<WriteOutcome> FailAsync(Failure request, CancellationToken ct) => inner.FailAsync(request, ct);
        public Task<ClassMapping?> GetClassMappingAsync(string model, string name, CancellationToken ct) => inner.GetClassMappingAsync(model, name, ct);
        public async Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess request, CancellationToken ct)
        {
            var outcome = await inner.ClassifierSucceededAsync(request, ct);
            stop.Cancel();
            return outcome;
        }
    }
    [Test]
    public async Task Map_resume_and_GET_of_older_run_keep_manifest_type_after_a_new_run_starts()
    {
        Responses("MANIFEST"); f.Polls.Clear();
        var request = SqlDatabase.Request("AUTO"); await db.Store.IntakeAsync(request, default);
        var job = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync))!;
        var classifier = await db.Accepted(job, "classify", "doc-router-v1");
        await db.Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, classifier, "doc-router-v1", 2, f.Clock.GetUtcNow(),
            "cargo-manifest", .99m, null, null, Guid.NewGuid(), "{}", "MANIFEST"), default);
        var extraction = await db.Accepted(job, "extract", "manifest-v1");
        await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, extraction, "manifest-v1", 2, f.Clock.GetUtcNow(), "raw-path"), default);
        await Processor().ExecuteAsync(job, default);
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:", StringComparison.Ordinal));
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"));
        await db.Store.IntakeAsync(request with { ClientRequestId = "next-run", Reprocess = true }, default);
        var next = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync))!;
        var op = await db.Accepted(next, "classify", "doc-router-v1");
        await db.Store.ClassifierSucceededAsync(new(next.JobId, next.LeaseToken, op, "doc-router-v1", 2, f.Clock.GetUtcNow(),
            "bill-of-lading", .99m, null, null, Guid.NewGuid(), "{}", "BL"), default);
        var response = await new GetJob(db.Store).ExecuteAsync(job.JobId, new(1, "TEST", "TEST", [], null, 10, 100, true), 1, default);
        Assert.Equal("AUTO", response.RequestedDocumentType); Assert.Equal("MANIFEST", response.DocumentType);
        Assert.Equal(ResultTypes.Manifest, response.ResultType); Assert.IsType<CanonicalManifestResult>(response.Result);
        var current = (await db.Store.GetRunAsync(job.JobId, 1, null, default))!;
        Assert.Equal("BL", current.ResolvedDocumentTypeCode);
        var payload = await db.Scalar<string>("SELECT PayloadJson FROM dbo.WebhookDeliveries WHERE JobId=@JobId AND RunNo=1", job);
        var context = (await db.Store.GetEventContextAsync(job.JobId, 1, default))!;
        using var body = JsonDocument.Parse(DeliverWebhook.BuildBody(payload, context.ResultJson, new ISTDocAi.Infrastructure.Services.DocAi.Webhooks.DocAiV2Json()));
        Assert.Equal("MANIFEST", body.RootElement.GetProperty("Job").GetProperty("DocumentType").GetString());
        Assert.Equal(ResultTypes.Manifest, body.RootElement.GetProperty("Job").GetProperty("ResultType").GetString());
    }
}
