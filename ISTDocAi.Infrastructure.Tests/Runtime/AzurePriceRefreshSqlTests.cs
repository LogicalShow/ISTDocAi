using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Tests.Pricing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
[Category("Sql")]
public sealed class AzurePriceRefreshSqlTests
{
    private SqlDatabase db=null!;
    private SqlAzurePriceCheckRepository store=null!;
    [SetUp]
    public async Task Setup()
    {
        SqlTestGuard.Require();db=new SqlDatabase();await db.InitializeAsync();
        var options=Options.Create(new DocAiSqlOptions { ConnectionString=db.ConnectionString });
        store=new(new SqlExecution(new SqlConnectionFactory(options),new SqlStatementCatalog(),options));
        await db.Execute("DELETE dbo.AzureMeterPrices; INSERT dbo.AzureMeterPrices VALUES ('DI_CUSTOM_EXTRACT','2024-01-01',30,'USD',N'baseline'),('DI_CUSTOM_CLASSIFY','2024-01-01',3,'USD',N'baseline');");
    }
    [TearDown] public async Task Cleanup() { if(db is not null)await db.DisposeAsync(); }
    private static AzurePriceCheck Check(decimal extract=30,decimal classify=3,string? reason=null)
    {
        var now=DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(),now,now,reason is null?"NoChange":"Refused",reason,
            AzurePricePolicy.Evaluate([AzurePricePolicyTests.Row() with { Price=extract },AzurePricePolicyTests.Row(false) with { Price=classify }],now));
    }
    [Test]
    public async Task No_change_records_two_history_rows_and_latest_time()
    {
        var check=Check();var result=await store.SaveAsync(check,default);
        Assert.That(result.All(x=>x.Outcome=="NoChange"),Is.True);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzurePriceChecks"),Is.EqualTo(2));
        Assert.That(await store.LatestCompletedAsync(default),Is.Not.Null);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices"),Is.EqualTo(2));
    }
    [Test]
    public async Task Changed_pair_appends_tomorrow_UTC_and_replay_returns_persisted_results()
    {
        var check=Check(300,1);var first=await store.SaveAsync(check,default);var replay=await store.SaveAsync(check,default);
        Assert.That(replay,Is.EqualTo(first));Assert.That(first.All(x=>x.Outcome=="Updated"),Is.True);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices WHERE EffectiveFrom=DATEADD(day,1,CONVERT(date,SYSUTCDATETIME()))"),Is.EqualTo(2));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzurePriceChecks"),Is.EqualTo(2));
        Assert.That((await store.SaveAsync(Check(300,1),default)).All(x=>x.Outcome=="NoChange"),Is.True);
    }
    [TestCase("tomorrow","EXISTING_EFFECTIVE_DATE_CONFLICT")]
    [TestCase("future","FUTURE_PRICE_SCHEDULE_EXISTS")]
    [TestCase("baseline","NO_CURRENT_BASELINE")]
    public async Task Pair_wide_refusal_preserves_manual_rows(string scenario,string reason)
    {
        if(scenario=="baseline")await db.Execute("DELETE dbo.AzureMeterPrices WHERE MeterCode='DI_CUSTOM_CLASSIFY'");
        else await db.Execute("INSERT dbo.AzureMeterPrices VALUES ('DI_CUSTOM_CLASSIFY',DATEADD(day,@days,CONVERT(date,SYSUTCDATETIME())),99,'USD',N'manual')",new { days=scenario=="tomorrow"?1:2 });
        var count=await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices");
        var results=await store.SaveAsync(Check(31,4),default);
        Assert.That(results.All(x=>x.Outcome=="Refused"&&x.ReasonCode==reason),Is.True);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices"),Is.EqualTo(count));
    }
    [TestCase("Refused")][TestCase("Failed")]
    public async Task History_only_never_inserts_prices_and_fingerprint_ignores_check_times(string outcome)
    {
        var a=Check(reason:"INVALID_METER") with { Outcome=outcome };
        await store.SaveAsync(a,default);await store.SaveAsync(a with { CheckId=Guid.NewGuid(),CheckedAtUtc=a.CheckedAtUtc.AddMinutes(1) },default);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices"),Is.EqualTo(2));
        Assert.That(await db.Scalar<int>("SELECT COUNT(DISTINCT WarningFingerprint) FROM dbo.AzurePriceChecks"),Is.EqualTo(2));
    }
    [Test]
    public async Task Concurrent_checks_serialize_and_insert_each_price_once()
    {
        var results=await Task.WhenAll(store.SaveAsync(Check(31,4),default),store.SaveAsync(Check(31,4),default));
        Assert.That(results.SelectMany(x=>x).Count(x=>x.Outcome=="Updated"),Is.EqualTo(2));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureMeterPrices"),Is.EqualTo(4));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzurePriceChecks"),Is.EqualTo(4));
    }

}
