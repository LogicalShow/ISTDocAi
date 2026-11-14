using NUnit.Framework;
using ISTDocAi.Application.Services.DocAi.Intake;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Reflection;
using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class RetentionOptionsTests
{
    
    [TestCase(59, false)] [TestCase(60, true)] [TestCase(1800, true)] [TestCase(7200, true)] [TestCase(7201, false)]
    public void Timeout_range_is_validated(int seconds, bool valid)
    {
        var services = new ServiceCollection();
        services.AddDocAiSql(o => { o.ConnectionString = "Server=test"; o.RetentionTimeoutSeconds = seconds; });
        using var provider = services.BuildServiceProvider();
        if (valid) Assert.Equal(seconds, provider.GetRequiredService<IOptions<DocAiSqlOptions>>().Value.RetentionTimeoutSeconds);
        else Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DocAiSqlOptions>>().Value);
        Assert.Equal(1800, new DocAiSqlOptions().RetentionTimeoutSeconds);
    }
    [Test]
    public void Only_S15_uses_retention_timeout_and_all_commands_keep_cancellation()
    {
        var options = Options.Create(new DocAiSqlOptions { CommandTimeoutSeconds = 17, RetentionTimeoutSeconds = 1234 });
        var sql = new SqlStatementCatalog(); var store = new SqlDocAiStore(new SqlConnectionFactory(options), sql, options);
        using var stop = new CancellationTokenSource();
        foreach (var label in sql.Statements.Keys)
        {
            var command = (CommandDefinition)typeof(SqlDocAiStore).GetMethod("Command", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(store, [label, null, stop.Token])!;
            Assert.Equal(label == "S15" ? 1234 : 17, command.CommandTimeout);
            Assert.Equal(stop.Token, command.CancellationToken);
        }
    }
}

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class RetentionSqlIntegrationTests 
{
    private readonly SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private async Task<Guid> Completed()
    {
        var job = await db.NewClaim(); var operation = await db.Accepted(job);
        await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2, DateTimeOffset.UtcNow, "raw"), default);
        await db.Store.CompleteAsync(new(job.JobId, job.LeaseToken, "{}", 0, Guid.NewGuid(), "{}"), default);
        return job.JobId;
    }
    private Task<string> AuditSnapshot() => db.Scalar<string>("""
        SELECT (SELECT * FROM dbo.JobRuns ORDER BY JobId,RunNo FOR JSON PATH, INCLUDE_NULL_VALUES) runs,
        (SELECT * FROM dbo.AzureOperations ORDER BY OperationId FOR JSON PATH, INCLUDE_NULL_VALUES) operations,
        (SELECT * FROM dbo.UsageLedger ORDER BY UsageId FOR JSON PATH, INCLUDE_NULL_VALUES) ledger
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        """);
    [Test, Category("Sql")]
    public async Task Cutoffs_soft_delete_live_jobs_delivery_states_audit_records_and_idempotence()
    {
        SqlTestGuard.Require();
        var old = await Completed(); var recent = await Completed();
        var failed = await db.NewClaim();
        await db.Store.FailAsync(new(failed.JobId, failed.LeaseToken, "FAILED", null, false, Guid.NewGuid(), "{}"), default);
        var processing = await db.NewClaim();
        await db.Store.IntakeAsync(SqlDatabase.Request(), default);
        await db.Execute("""
            UPDATE dbo.Jobs SET CreatedAtUtc=DATEADD(month,-14,SYSUTCDATETIME()),CompletedAtUtc=DATEADD(month,-14,SYSUTCDATETIME()) WHERE JobId<>@recent;
            UPDATE dbo.JobResults SET CreatedAtUtc=DATEADD(month,-14,SYSUTCDATETIME()) WHERE JobId=@old;
            UPDATE dbo.JobSubmissions SET CreatedAtUtc=DATEADD(month,-14,SYSUTCDATETIME()) WHERE JobId=@old;
            UPDATE dbo.WebhookDeliveries SET Status='Delivered', CreatedAtUtc=DATEADD(day,-100,SYSUTCDATETIME()), DeliveredAtUtc=DATEADD(day,-91,SYSUTCDATETIME()) WHERE JobId=@old;
            UPDATE dbo.WebhookDeliveries SET Status='Delivered', CreatedAtUtc=DATEADD(day,-100,SYSUTCDATETIME()), DeliveredAtUtc=DATEADD(day,-89,SYSUTCDATETIME()) WHERE JobId=@recent;
            UPDATE dbo.WebhookDeliveries SET Status='Superseded', CreatedAtUtc=DATEADD(day,-91,SYSUTCDATETIME()) WHERE JobId=@failed;
            INSERT dbo.WebhookDeliveries(EventId,JobId,RunNo,ConsumerId,EventType,Status,PayloadJson,CreatedAtUtc)
            VALUES(NEWID(),@processing,1,1,'job.failed','Pending','{}',DATEADD(day,-100,SYSUTCDATETIME())),
                  (NEWID(),@processing,1,1,'job.completed','DeadLettered','{}',DATEADD(day,-100,SYSUTCDATETIME()));
            """, new { old, recent, failed = failed.JobId, processing = processing.JobId });
        await db.Execute("""
            UPDATE dbo.JobRuns SET CreatedAtUtc=DATEADD(month,-14,SYSUTCDATETIME());
            UPDATE dbo.AzureOperations SET SentAtUtc=DATEADD(month,-14,SYSUTCDATETIME()),CompletedAtUtc=DATEADD(month,-14,SYSUTCDATETIME());
            UPDATE dbo.UsageLedger SET OccurredAtUtc=DATEADD(month,-14,SYSUTCDATETIME()),RecordedAtUtc=DATEADD(month,-14,SYSUTCDATETIME());
            """);
        var audit = await AuditSnapshot();
        var live = await db.Scalar<string>("SELECT * FROM dbo.Jobs WHERE Status IN ('Queued','Processing') ORDER BY JobSeq FOR JSON PATH, INCLUDE_NULL_VALUES");
        await db.Store.RunAsync(default);
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobResults WHERE JobId=@old", new { old }));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobResults WHERE JobId=@recent", new { recent }));
        Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions WHERE JobId=@old", new { old }));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions WHERE JobId=@recent", new { recent }));
        Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE IsDeleted=1 AND FileName IS NULL"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE JobId=@recent AND IsDeleted=0 AND FileName IS NOT NULL", new { recent }));
        Assert.Equal(live, await db.Scalar<string>("SELECT * FROM dbo.Jobs WHERE Status IN ('Queued','Processing') ORDER BY JobSeq FOR JSON PATH, INCLUDE_NULL_VALUES"));
        Assert.Equal(3, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE Status='Pending'"));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries WHERE Status='DeadLettered'"));
        Assert.Equal(audit, await AuditSnapshot());
        Assert.Null(await db.Store.GetRunAsync(old, 1, null, default));
        var consumer = Assert.Single(await db.Store.GetConsumersAsync(default));
        var error = await Assert.ThrowsAsync<IntakeException>(() => new GetJob(db.Store).ExecuteAsync(old, consumer, null, default));
        Assert.Equal(404, error.Status);
        var snapshot = await db.Snapshot();
        var submissions = await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions");
        await db.Store.RunAsync(default);
        Assert.Equal(snapshot, await db.Snapshot());
        Assert.Equal(submissions, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions"));
    }
    [Test, Category("Sql")]
    public async Task More_than_5000_submissions_are_purged_with_dedicated_timeout()
    {
        SqlTestGuard.Require();
        var job = await Completed();
        await db.Execute("""
            WITH numbers AS (SELECT TOP (5001) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
            INSERT dbo.JobSubmissions(ConsumerId,ClientRequestId,JobId,RunNo,CreatedAtUtc)
            SELECT 1, CONCAT('old-',n),@job,1,DATEADD(month,-14,SYSUTCDATETIME()) FROM numbers;
            """, new { job });
        Assert.Equal(5002, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions"));
        // S15 must outlive a deliberately held lock beyond the ordinary one-second command timeout.
        var options = Options.Create(new DocAiSqlOptions { ConnectionString = db.ConnectionString, CommandTimeoutSeconds = 1, RetentionTimeoutSeconds = 60 });
        await using var blocker = new Microsoft.Data.SqlClient.SqlConnection(db.ConnectionString); await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await blocker.ExecuteAsync("UPDATE dbo.JobSubmissions SET CreatedAtUtc=CreatedAtUtc WHERE ClientRequestId='old-1'", transaction: transaction);
        var store = new SqlDocAiStore(new SqlConnectionFactory(options), new SqlStatementCatalog(), options);
        var purge = store.RunAsync(default);
        try { await Task.Delay(TimeSpan.FromSeconds(2)); }
        finally { await transaction.RollbackAsync(); }
        await purge;
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobSubmissions"));
    }
}

