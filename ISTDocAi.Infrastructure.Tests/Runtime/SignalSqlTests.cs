using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class SignalSqlTests 
{
    private readonly SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();

    [Test, Category("Sql")]
    public async Task Queue_age_ignores_future_backoff_deleted_and_live_leases_but_counts_expired_leases()
    {
        SqlTestGuard.Require();
        Assert.Null(await db.Store.OldestDueSecondsAsync(default));
        await db.NewClaim();
        Assert.Null(await db.Store.OldestDueSecondsAsync(default));
        await db.Execute("UPDATE dbo.Jobs SET LeaseExpiresAtUtc=DATEADD(second,-400,SYSUTCDATETIME())");
        Assert.InRange((await db.Store.OldestDueSecondsAsync(default))!.Value, 400, 410);
        await db.Execute("UPDATE dbo.Jobs SET IsDeleted=1");
        Assert.Null(await db.Store.OldestDueSecondsAsync(default));
        await db.Execute("UPDATE dbo.Jobs SET IsDeleted=0,Status='Queued',LeaseToken=NULL,LeaseExpiresAtUtc=NULL,NextAttemptAtUtc=DATEADD(hour,1,SYSUTCDATETIME())");
        Assert.Null(await db.Store.OldestDueSecondsAsync(default));
        await db.Execute("UPDATE dbo.Jobs SET NextAttemptAtUtc=DATEADD(second,-350,SYSUTCDATETIME())");
        Assert.InRange((await db.Store.OldestDueSecondsAsync(default))!.Value, 350, 360);
    }
    [Test, Category("Sql")]
    public async Task Missing_price_aggregates_pages_then_clears_when_date_is_priced()
    {
        SqlTestGuard.Require();
        Assert.Empty(await db.Store.UnpricedUsageAsync(default));
        // The schema seeds example prices; remove them only in this generated test database.
        await db.Execute("DELETE dbo.AzureMeterPrices");
        var job = await db.NewClaim();
        var id = await db.Accepted(job);
        await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, id, "bl-v1", 2, DateTimeOffset.UtcNow, "raw"), default);
        var usage = Assert.Single(await db.Store.UnpricedUsageAsync(default));
        Assert.Equal(1, usage.UnpricedRows);
        Assert.Equal(2, usage.UnpricedPages);
        await db.Execute("INSERT dbo.AzureMeterPrices(MeterCode,EffectiveFrom,PricePer1000,Source) VALUES(@meter,'2020-01-01',30,N'Signal integration test')", new { meter = usage.MeterCode });
        Assert.Empty(await db.Store.UnpricedUsageAsync(default));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger WHERE PricePer1000 IS NULL"));
    }
}
