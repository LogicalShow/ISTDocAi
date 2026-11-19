using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using ISTDocAi.Tools.Tests.Provisioning;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

// Inherits Sql opt-in guard. All fixture mutations target a newly migrated disposable database only.
public sealed partial class OperatorSqlTests
{
    private static async Task<JsonNode> AzureReport(ProvisioningDatabase db, string consumer = "") =>
        JsonNode.Parse(await db.Scalar<string>(AzureCostReports.Sql(consumer, ReportRangeTests.Period())))!;
    private static decimal Money(JsonNode row, string key) => row[key]!.GetValue<decimal>();
    private static async Task AzureSeed(ProvisioningDatabase db)
    {
        await Seed(db);
        await db.Execute(ModelSql.Bl("fixture-bl", .99m, json: OperatorPureTests.Mapping("BL").ToJsonString()));
        await db.Execute("""
            INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1)
            VALUES('SECOND','Second','FWD','SECOND',HASHBYTES('SHA2_256','second-fixture'));
            DELETE dbo.AzureMeterPrices;
            INSERT dbo.AzureMeterPrices(MeterCode,EffectiveFrom,PricePer1000,Source) VALUES
            ('DI_CUSTOM_EXTRACT','2026-01-01',30,'historic fixture'),
            ('DI_CUSTOM_CLASSIFY','2026-01-01',3,'historic classify'),
            ('DI_CUSTOM_EXTRACT',CAST(SYSUTCDATETIME() AS date),40,'current fixture'),
            ('DI_CUSTOM_EXTRACT',DATEADD(day,1,CAST(SYSUTCDATETIME() AS date)),50,'scheduled fixture');
            """);
        await db.Execute(PageSql.Generate(Request() with { Consumer = "SECOND", Pages = 100 }));
    }
    // UsageLedger has no protection triggers in InitialSchema or later SQL migrations; runtime append-only is permissions based.
    // Charge backdating/deletion uses Backdate's disable/enable pattern; no product SQL is altered.
    private static async Task AzureUsage(ProvisioningDatabase db, short consumer, short pages, DateTime occurred, decimal? price, bool charge, DateTime chargeAt)
    {
        await RangeCharge(db, consumer, pages, chargeAt);
        var job = await db.Scalar<Guid>($"SELECT TOP(1) JobId FROM dbo.CustomerPageCharges WHERE ConsumerId={consumer} ORDER BY ChargeId DESC");
        await db.Execute($"UPDATE dbo.UsageLedger SET OccurredAtUtc='{Instant(occurred)}',PricePer1000={(price.HasValue ? price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL")} WHERE JobId='{job}'");
        if (!charge) await Backdate(db, $"DELETE dbo.CustomerPageCharges WHERE JobId='{job}'");
    }
    [Test]
    public async Task Azure_cost_cohorts_recovery_unresolved_filter_and_aggregate_ratio()
    {
        await using var db = new ProvisioningDatabase(); await AzureSeed(db);
        var p = ReportRangeTests.Period();
        var first = await db.Scalar<short>("SELECT ConsumerId FROM dbo.Consumers WHERE Code='FWD-UNF'");
        var second = await db.Scalar<short>("SELECT ConsumerId FROM dbo.Consumers WHERE Code='SECOND'");
        await AzureUsage(db, first, 2, p.FromUtc.AddDays(-1), 30, true, p.FromUtc);
        await AzureUsage(db, second, 8, p.FromUtc, 60, true, p.FromUtc);
        // Charged outside the range must be excluded from both populations even with usage inside.
        await AzureUsage(db, first, 5, p.FromUtc, 30, true, p.ToExclusiveUtc);
        await AzureUsage(db, first, 3, p.FromUtc, null, false, p.FromUtc);
        await AzureUsage(db, first, 4, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc), null, true, p.FromUtc);
        var report = await AzureReport(db); var totals = report["totals"]!;
        Assert.That(totals["chargedRuns"]!.GetValue<long>(), Is.EqualTo(3));
        Assert.That(totals["chargedPages"]!.GetValue<long>(), Is.EqualTo(14));
        Assert.That(Money(totals,"chargedFrozenCostUsd"), Is.EqualTo(.54m));
        Assert.That(Money(totals,"unchargedRecoveredEstimateUsd"), Is.EqualTo(.09m));
        Assert.That(totals["chargedUnresolvedPages"]!.GetValue<long>(), Is.EqualTo(4));
        Assert.That(Money(totals,"costPerChargedPageUsd"), Is.EqualTo(.54m/14).Within(.0000000001m));
        var filtered = await AzureReport(db,"SECOND");
        Assert.That(((JsonArray)filtered["consumers"]!).Count, Is.EqualTo(1));
        Assert.That(Money(filtered["totals"]!,"costPerChargedPageUsd"), Is.EqualTo(.06m));
        Assert.That(((JsonArray)report["pricesUsed"]!).Any(x => x!["pricePer1000"]!.GetValue<decimal>() == 60), Is.True);
    }
    [Test]
    public async Task Azure_cost_uncharged_only_null_ratio_current_and_scheduled_prices()
    {
        await using var db = new ProvisioningDatabase(); await AzureSeed(db); var p = ReportRangeTests.Period();
        var id = await db.Scalar<short>("SELECT ConsumerId FROM dbo.Consumers WHERE Code='FWD-UNF'");
        await AzureUsage(db,id,3,p.FromUtc,null,false,p.FromUtc);
        var report = await AzureReport(db);
        Assert.That(report["totals"]!["costPerChargedPageUsd"], Is.Null);
        Assert.That(Money(report["totals"]!,"unchargedEstimatedCostUsd"), Is.EqualTo(.09m));
        var current = ((JsonArray)report["currentPriceList"]!).Single(x => x!["meterCode"]!.ToString()=="DI_CUSTOM_EXTRACT")!;
        Assert.That(Money(current,"pricePer1000"), Is.EqualTo(40));
        Assert.That(((JsonArray)report["scheduledPrices"]!).Count, Is.EqualTo(1));
        Assert.That(Money(report["scheduledPrices"]![0]!,"pricePer1000"), Is.EqualTo(50));
    }
    [Test]
    public async Task Azure_cost_reversal_sign_and_charged_recovered_estimate()
    {
        await using var db = new ProvisioningDatabase(); await AzureSeed(db); var p = ReportRangeTests.Period();
        var id = await db.Scalar<short>("SELECT ConsumerId FROM dbo.Consumers WHERE Code='FWD-UNF'");
        await AzureUsage(db,id,2,p.FromUtc,null,true,p.FromUtc);
        await db.Execute("""
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,ReversesUsageId,CorrelationId)
            SELECT ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,-Pages,PricePer1000,OccurredAtUtc,UsageId,'reversal-fixture'
            FROM dbo.UsageLedger;
            """);
        var report = await AzureReport(db);
        Assert.That(Money(report["totals"]!,"chargedRecoveredEstimateUsd"), Is.Zero);
        Assert.That(report["totals"]!["chargedAzureExtractPages"]!.GetValue<long>(), Is.Zero);
        Assert.That(report["totals"]!["chargedPages"]!.GetValue<long>(), Is.EqualTo(2));
    }
    [Test]
    public async Task Azure_cost_unknown_consumer_50018()
    {
        await using var db = new ProvisioningDatabase(); await Seed(db);
        var error = Assert.ThrowsAsync<SqlException>(async () => await AzureReport(db,"UNKNOWN"));
        Assert.That(error!.Number, Is.EqualTo(50018));
    }
}
