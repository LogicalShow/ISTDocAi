using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using ISTDocAi.Tools.Tests.Provisioning;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

public sealed partial class OperatorSqlTests
{
    private static async Task<AzurePriceStatus> PriceStatus(ProvisioningDatabase db) => new(JsonNode.Parse(await db.Scalar<string>(AzurePriceStatus.Sql))!, new AzurePriceStatusTests.Clock(AzurePriceStatusTests.Now));
    private static Task PricePair(ProvisioningDatabase db,string outcome,string at,string reason="PRICE_SOURCE_FAILED") => db.Execute($"""
        DECLARE @id uniqueidentifier=NEWID();
        INSERT dbo.AzurePriceChecks(CheckId,MeterCode,StartedAtUtc,CheckedAtUtc,Outcome,ReasonCode,CurrentPrice,ObservedPrice,ProposedEffectiveFrom)
        SELECT @id,MeterCode,'{at}','{at}','{outcome}','{reason}',30,35,'2026-11-17'
        FROM (VALUES('DI_CUSTOM_EXTRACT'),('DI_CUSTOM_CLASSIFY')) m(MeterCode);
        """);
    [Test]
    public async Task Price_status_warning_run_resolution_last_change_and_schedule()
    {
        await using var db=new ProvisioningDatabase(); await Seed(db);
        await PricePair(db,"Updated","2026-11-16T01:00:00");
        await PricePair(db,"Refused","2026-11-16T02:00:00","EXISTING_EFFECTIVE_DATE_CONFLICT");
        await PricePair(db,"Failed","2026-11-16T03:00:00");
        await db.Execute("INSERT dbo.AzureMeterPrices(MeterCode,EffectiveFrom,PricePer1000,Source) VALUES('DI_CUSTOM_EXTRACT',DATEADD(day,10,CAST(SYSUTCDATETIME() AS date)),35,'scheduled test')");
        var s=await PriceStatus(db);
        Assert.That(s.Warnings()[0],Does.Contain("since 2026-11-16 02:00 UTC"));
        Assert.That(s.Render(),Does.Contain("30.0000 -> 35.0000").And.Contain("scheduled test"));
        Assert.That(s.Stale,Is.False);
        await PricePair(db,"NoChange","2026-11-16T04:00:00");
        Assert.That((await PriceStatus(db)).Warnings(),Is.Empty);
    }
    [Test]
    public async Task Price_status_stale_empty_and_missing_table()
    {
        await using var db=new ProvisioningDatabase(); await Seed(db);
        Assert.That((await PriceStatus(db)).NeverRan,Is.True);
        await PricePair(db,"NoChange","2026-11-14T08:00:00");
        Assert.That((await PriceStatus(db)).Stale,Is.True);
        await db.Execute("DROP TABLE dbo.AzurePriceChecks");
        var s=await PriceStatus(db); Assert.That(s.HistoryAvailable,Is.False); Assert.That(s.Warnings(),Is.Empty);
    }
    [Test]
    public async Task Price_status_denied_history_returns_clear_message()
    {
        await using var db=new ProvisioningDatabase(); await Seed(db);
        await db.Execute("CREATE USER price_reader WITHOUT LOGIN; GRANT SELECT ON dbo.AzureMeterPrices TO price_reader; GRANT VIEW DEFINITION ON dbo.AzurePriceChecks TO price_reader; DENY SELECT ON dbo.AzurePriceChecks TO price_reader;");
        var json=await db.Scalar<string>("EXECUTE AS USER='price_reader'; " + AzurePriceStatus.Sql + " REVERT;");
        var s=new AzurePriceStatus(JsonNode.Parse(json)!);
        Assert.That(s.Banner(),Is.Empty); Assert.That(s.Render(),Does.Contain("check SELECT permission"));
    }
}
