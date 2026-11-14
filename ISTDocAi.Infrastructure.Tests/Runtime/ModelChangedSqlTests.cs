using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ModelChangedSqlTests 
{
    private SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();

    private async Task<IntakeRequest> Completed(bool auto = false, string? line = null)
    {
        if (auto)
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('router-v1','AUTO','classify',1,'Active')");
        if (line is not null)
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status) VALUES('bl-line-v1','BL','LINE','extract',1,'Active')");
        var request = SqlDatabase.Request(auto ? "AUTO" : "BL");
        await db.Store.IntakeAsync(request, default);
        var job = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        if (auto)
        {
            var classify = await db.Accepted(job, "classify", "router-v1");
            Assert.Equal(WriteOutcome.Applied, await db.Store.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken,
                classify, "router-v1", 2, DateTimeOffset.UtcNow, "bl", .99m, line, null, Guid.NewGuid(), "{}", "BL"), default));
        }
        await Finish(job, line is null ? "bl-v1" : "bl-line-v1");
        return request;
    }

    private async Task Finish(ClaimedJob job, string model = "bl-v1")
    {
        var operation = await db.Accepted(job, "extract", model);
        Assert.Equal(WriteOutcome.Applied, await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken,
            operation, model, 2, DateTimeOffset.UtcNow, "raw.json"), default));
        Assert.Equal(WriteOutcome.Applied, await db.Store.CompleteAsync(new(job.JobId, job.LeaseToken, "{}", 0,
            Guid.NewGuid(), "{}", MappingFingerprint: InMemoryMappingProfileResolver.Fingerprint(new(), model, "BL")), default));
    }

    
    [TestCase("none")]
    [TestCase("extractModel")]
    [TestCase("classifier")]
    [TestCase("mapping")]
    [TestCase("legacy")]
    public async Task Completed_run_compares_each_input_and_replays_keep_their_bound_run(string change)
    {
        SqlTestGuard.Require();
        var original = await Completed(change == "classifier");
        switch (change)
        {
            case "extractModel":
                await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='bl-v1'; INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('bl-v2','BL','extract',2,'Active')");
                break;
            case "classifier":
                await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='router-v1'; INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('router-v2','AUTO','classify',2,'Active')");
                break;
            case "mapping": await db.Execute("UPDATE dbo.JobRuns SET MappingFingerprint=REPLICATE('a',64)"); break;
            case "legacy": await db.Execute("UPDATE dbo.JobRuns SET MappingFingerprint=NULL"); break;
        }
        // Even after a change, replaying the original request never creates a run.
        Assert.Equal((short)1, (await db.Store.IntakeAsync(original, default)).RunNo);
        var next = original with { ClientRequestId = "new-request", BlobPath = null };
        var probe = await db.Store.IntakeAsync(next, default);
        if (change == "none")
        {
            Assert.Equal(IntakeOutcome.Existing, probe.Outcome);
            Assert.Equal("New", probe.RunReason);
            Assert.Null(probe.ChangesJson);
            Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations"));
            return;
        }
        Assert.Equal(IntakeOutcome.NeedsUpload, probe.Outcome);
        var result = await db.Store.IntakeAsync(next with { BlobPath = "new.pdf" }, default);
        Assert.Equal((short)2, result.RunNo);
        Assert.Equal("ModelChanged", result.RunReason);
        using var changes = JsonDocument.Parse(result.ChangesJson!);
        if (change is "mapping" or "legacy") Assert.True(changes.RootElement.GetProperty("mapping").GetBoolean());
        else
        {
            var ids = changes.RootElement.GetProperty(change);
            Assert.Equal(change == "classifier" ? "router-v1" : "bl-v1", ids.GetProperty("oldModelId").GetString());
            Assert.Equal(change == "classifier" ? "router-v2" : "bl-v2", ids.GetProperty("newModelId").GetString());
            Assert.False(changes.RootElement.TryGetProperty("mapping", out _));
        }
        var run = (await db.Store.GetRunAsync(result.JobId!.Value, 1, 2, default))!;
        Assert.Equal(result.ChangesJson, run.ChangesJson);
        Assert.Equal("ModelChanged", run.RunReason);
        // Queued and Processing still return Existing even when active inputs differ.
        Assert.Equal(IntakeOutcome.Existing, (await db.Store.IntakeAsync(next with { ClientRequestId = "queued" }, default)).Outcome);
        var job = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
        Assert.Equal(IntakeOutcome.Existing, (await db.Store.IntakeAsync(next with { ClientRequestId = "processing" }, default)).Outcome);
        Assert.Equal((short)1, (await db.Store.IntakeAsync(original, default)).RunNo);
        await db.Store.FailAsync(new(job.JobId, job.LeaseToken, "FAILED", null, false, Guid.NewGuid(), "{}"), default);
        Assert.Equal("Requeue", (await db.Store.IntakeAsync(next with { ClientRequestId = "failed", BlobPath = "retry.pdf" }, default)).RunReason);
    }

    [Test, Category("Sql")]
    public async Task Concurrent_changed_submissions_create_one_run_and_one_new_ledger_set()
    {
        SqlTestGuard.Require();
        var original = await Completed();
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='bl-v1'; INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('bl-v2','BL','extract',2,'Active')");
        var one = original with { ClientRequestId = "parallel-1", BlobPath = null };
        var two = original with { ClientRequestId = "parallel-2", BlobPath = null };
        var probes = await Task.WhenAll(db.Store.IntakeAsync(one, default), db.Store.IntakeAsync(two, default));
        Assert.All(probes, p => Assert.Equal(IntakeOutcome.NeedsUpload, p.Outcome));
        var results = await Task.WhenAll(db.Store.IntakeAsync(one with { BlobPath = "one.pdf" }, default),
            db.Store.IntakeAsync(two with { BlobPath = "two.pdf" }, default));
        Assert.Single(results, r => r.Outcome == IntakeOutcome.Requeued);
        Assert.Single(results, r => r.Outcome == IntakeOutcome.Existing);
        Assert.All(results, r => Assert.Equal((short)2, r.RunNo));
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"));
        var claims = await Task.WhenAll(db.Store.ClaimAsync(default), db.Store.ClaimAsync(default));
        var claim = Assert.Single(claims, c => c is not null)!;
        await Finish(claim, "bl-v2");
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations WHERE RunNo=2"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE RunNo=2"));
        Assert.Equal(4, await db.Store.DailyPagesAsync(1, default));
        Assert.Equal(IntakeOutcome.Existing, (await db.Store.IntakeAsync(one with { ClientRequestId="after-complete" }, default)).Outcome);
        Assert.Equal("ModelChanged", (await db.Store.IntakeAsync(two, default)).RunReason);
    }

    [Test, Category("Sql")]
    public async Task AUTO_compares_the_resolved_document_type_and_line_not_the_requested_AUTO_route()
    {
        SqlTestGuard.Require();
        var original = await Completed(auto: true, line: "LINE");
        Assert.Equal(IntakeOutcome.Existing, (await db.Store.IntakeAsync(original with { ClientRequestId="same-line" }, default)).Outcome);
        // Changing the generic extractor is irrelevant to this resolved LINE route.
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='bl-v1'; INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('bl-v2','BL','extract',2,'Active')");
        Assert.Equal(IntakeOutcome.Existing, (await db.Store.IntakeAsync(original with { ClientRequestId="generic-change" }, default)).Outcome);
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='bl-line-v1'; INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status) VALUES('bl-line-v2','BL','LINE','extract',2,'Active')");
        var changed = await db.Store.IntakeAsync(original with { ClientRequestId="line-change" }, default);
        Assert.Equal("ModelChanged", changed.RunReason);
        using var json = JsonDocument.Parse(changed.ChangesJson!);
        Assert.Equal("bl-line-v2", json.RootElement.GetProperty("extractModel").GetProperty("newModelId").GetString());
        Assert.False(json.RootElement.TryGetProperty("classifier", out _));
    }

    [Test, Category("Sql")]
    public async Task Changing_effective_field_map_without_changing_model_triggers_a_mapping_reread()
    {
        SqlTestGuard.Require();
        var original = await Completed();
        var mapping = new AnalyzeResultMappingOptions();
        mapping.FieldMap["billOfLadingNumber"] = "changed-field";
        var options = Microsoft.Extensions.Options.Options.Create(new DocAiSqlOptions { ConnectionString = db.ConnectionString });
        var store = new SqlDocAiStore(new SqlConnectionFactory(options), new SqlStatementCatalog(), options,
            Microsoft.Extensions.Options.Options.Create(mapping));
        var result = await store.IntakeAsync(original with { ClientRequestId="mapping-settings" }, default);
        Assert.Equal("ModelChanged", result.RunReason);
        Assert.Equal("{\"mapping\":true}", result.ChangesJson);
    }

    [Test, Category("Sql")]
    public async Task Explicit_reprocess_keeps_its_reason()
    {
        SqlTestGuard.Require();
        var original = await Completed();
        await db.Execute("UPDATE dbo.JobRuns SET MappingFingerprint=NULL");
        Assert.Equal("Reprocess", (await db.Store.IntakeAsync(original with { ClientRequestId="explicit", Reprocess=true }, default)).RunReason);
    }

    [Test, Category("Sql")]
    public async Task Mapping_upgrade_twice_is_a_noop_and_rejects_incompatible_columns_atomically()
    {
        SqlTestGuard.Require();
        await db.DisposeAsync();
        db = new SqlDatabase { UseSchemaScript = true };
        await db.InitializeAsync();
        var upgrade = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "05_run_mapping_fingerprint_upgrade.sql"));
        var original = await Completed();
        await db.Execute(upgrade);
        var before = await db.Snapshot();
        var constraint = await db.Scalar<int>("SELECT OBJECT_ID('dbo.CK_Runs_Reason')");
        await db.Execute(upgrade);
        Assert.Equal(before, await db.Snapshot());
        Assert.Equal(constraint, await db.Scalar<int>("SELECT OBJECT_ID('dbo.CK_Runs_Reason')"));
        Assert.Equal((short)1, (await db.Store.IntakeAsync(original, default)).RunNo);
        await db.Execute("ALTER TABLE dbo.JobRuns ALTER COLUMN MappingFingerprint varchar(64) NULL");
        Assert.Equal(50001, (await Assert.ThrowsAsync<SqlException>(() => db.Execute(upgrade))).Number);
        Assert.Equal(before, await db.Snapshot());
    }
}
