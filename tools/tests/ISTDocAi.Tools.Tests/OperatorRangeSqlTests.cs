using System.Globalization;
using System.Text.Json.Nodes;
using Dapper;
using ISTDocAi.OperatorTool;
using ISTDocAi.Tools.Tests.Provisioning;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

// Inherits Category Sql and the opt-in guard from OperatorSqlTests. Disposable databases only.
public sealed partial class OperatorSqlTests
{
    private static string Instant(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
    // Disposable test databases only: backdating fixture rows must bypass the append-only accounting triggers
    // (INSTEAD OF UPDATE/DELETE); they are re-enabled in the same batch.
    private const string UnlockAppendOnly = "DISABLE TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants; DISABLE TRIGGER dbo.TR_PageCharges_AppendOnly ON dbo.CustomerPageCharges;";
    private const string RelockAppendOnly = "ENABLE TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants; ENABLE TRIGGER dbo.TR_PageCharges_AppendOnly ON dbo.CustomerPageCharges;";
    private static Task Backdate(ProvisioningDatabase db, string sql) => db.Execute(UnlockAppendOnly + "\n" + sql + ";\n" + RelockAppendOnly);
    private static async Task RangeCharge(ProvisioningDatabase db, short consumer, short pages, DateTime at)
    {
        var intake = Intake(consumer, pages); await Reserve(db, intake);
        await db.Execute($"""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            EXEC dbo.AcquirePageQuotaLock {consumer};
            INSERT dbo.AzureOperations(JobId,RunNo,Kind,ModelId,State,OperationLocation,Pages,CompletedAtUtc)
            VALUES('{intake.JobId}',1,'extract','fixture-bl','Succeeded','https://fixture.invalid/operation',{pages},SYSUTCDATETIME());
            DECLARE @op bigint=SCOPE_IDENTITY();
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,CorrelationId)
            VALUES({consumer},'{intake.JobId}',1,@op,'DI_CUSTOM_EXTRACT','fixture-bl',{pages},30,SYSUTCDATETIME(),'fixture');
            UPDATE dbo.JobRuns SET Status='Completed',ResolvedDocumentTypeCode='BL' WHERE JobId='{intake.JobId}' AND RunNo=1;
            UPDATE dbo.Jobs SET Status='Completed' WHERE JobId='{intake.JobId}';
            EXEC dbo.SettlePageReservation '{intake.JobId}',1;
            {UnlockAppendOnly}
            UPDATE dbo.CustomerPageCharges SET RecordedAtUtc=CONVERT(datetime2(7),'{Instant(at)}',126) WHERE JobId='{intake.JobId}';
            {RelockAppendOnly}
            COMMIT;
            """);
    }
    private static async Task RangeSeed(ProvisioningDatabase db, ReportPeriod p)
    {
        await Seed(db);
        await db.Execute(ModelSql.Bl("fixture-bl", .99m, json: OperatorPureTests.Mapping("BL").ToJsonString()));
        var consumer = await db.Scalar<short>("SELECT ConsumerId FROM dbo.Consumers WHERE Code='FWD-UNF'");
        var original = await db.Scalar<long>("SELECT MIN(GrantId) FROM dbo.PageAllowanceGrants");
        await Backdate(db, $"UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc='{Instant(p.FromUtc.AddDays(-1))}' WHERE GrantId={original}");
        var topup = Request() with { Pages = 20 }; var adjustment = Request("Adjust") with { Pages = -3 }; var reversal = Request("Reverse") with { GrantId = original, AllowDebt = true };
        foreach (var (r, at) in new[] { (topup, p.FromUtc), (adjustment, p.FromUtc.AddMinutes(1)), (reversal, p.FromUtc.AddMinutes(2)) })
        {
            await db.Execute(PageSql.Generate(r));
            await Backdate(db, $"UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc='{Instant(at)}' WHERE RequestId='{r.RequestId}'");
        }
        await RangeCharge(db, consumer, 2, p.FromUtc.AddMilliseconds(-1));
        // Insert out of timestamp order; the two same-time rows must then sort by ChargeId.
        await RangeCharge(db, consumer, 1, p.FromUtc.AddHours(1));
        await RangeCharge(db, consumer, 3, p.FromUtc);
        await RangeCharge(db, consumer, 3, p.FromUtc);
        await RangeCharge(db, consumer, 5, p.ToExclusiveUtc);
        var excluded = Request() with { Pages = 50 };
        await db.Execute(PageSql.Generate(excluded));
        await Backdate(db, $"UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc='{Instant(p.ToExclusiveUtc)}' WHERE RequestId='{excluded.RequestId}'");
    }
    [Test]
    public async Task Date_statement_boundaries_signed_movements_reversal_and_independent_reconciliation()
    {
        await using var db = new ProvisioningDatabase(); var period = ReportRangeTests.Period(); await RangeSeed(db, period);
        var report = RangeReports.Statement(JsonNode.Parse(await db.Scalar<string>(RangeSql.Generate("FWD-UNF", period)))!, period);
        Assert.That(report["openingBalance"]!.GetValue<long>(), Is.EqualTo(98));
        Assert.That(report["topUpPages"]!.GetValue<long>(), Is.EqualTo(20));
        Assert.That(report["adjustmentPages"]!.GetValue<long>(), Is.EqualTo(-3));
        Assert.That(report["reversalPages"]!.GetValue<long>(), Is.EqualTo(-100));
        Assert.That(report["chargedPages"]!.GetValue<long>(), Is.EqualTo(7));
        Assert.That(report["chargedRuns"]!.GetValue<long>(), Is.EqualTo(3));
        Assert.That(report["closingBalance"]!.GetValue<long>(), Is.EqualTo(8));
        Assert.That(report["independentClosingBalance"]!.GetValue<long>(), Is.EqualTo(8));
        var movements = (JsonArray)report["movements"]!;
        Assert.That(movements.Select(x => x!["kind"]!.ToString()), Is.EqualTo(new[] { "TopUp", "Adjustment", "Reversal" }));
        Assert.That(RangeReports.Utc(movements[0]!["recordedAtUtc"]), Is.EqualTo(period.FromUtc));
        Assert.That(movements[2]!["originalPages"]!.GetValue<long>(), Is.EqualTo(100));
        Assert.That(RangeReports.Utc(movements[2]!["originalRecordedAtUtc"]), Is.EqualTo(period.FromUtc.AddDays(-1)));
        Assert.That(report.ToJsonString(), Does.Not.Match("(?i)usd|cost|price|azure|operatorIdentity|requestId|balanceAfter|\\\"note\\\""));
    }
    [Test]
    public async Task Date_documents_row_set_timestamp_then_charge_order_and_hourly_buckets()
    {
        await using var db = new ProvisioningDatabase(); var p = ReportRangeTests.Period("saudi"); await RangeSeed(db, p);
        await using var connection = new SqlConnection(db.ConnectionString);
        var rows = (await connection.QueryAsync(RangeSql.Generate("FWD-UNF", p, true))).ToArray();
        Assert.That(rows.Length, Is.EqualTo(3));
        Assert.That(rows.Select(r => (long)r.pages), Is.EqualTo(new long[] { 3, 3, 1 }));
        Assert.That((long)rows[0].chargeId, Is.LessThan((long)rows[1].chargeId));
        Assert.That((DateTime)rows[0].recordedAtUtc, Is.EqualTo(p.FromUtc));
        Assert.That(rows.All(r => (string)r.resolvedDocumentTypeCode == "BL" && (string)r.runReason == "New"), Is.True);
        var report = JsonNode.Parse(await db.Scalar<string>(RangeSql.Generate("FWD-UNF", p)))!;
        var hours = (JsonArray)report["usageByHour"]!;
        Assert.That(hours.Count, Is.EqualTo(2));
        Assert.That(RangeReports.Utc(hours[0]!["hourUtc"]), Is.EqualTo(p.FromUtc));
        Assert.That(hours[0]!["runs"]!.GetValue<long>(), Is.EqualTo(2));
        Assert.That(hours[0]!["pages"]!.GetValue<long>(), Is.EqualTo(6));
        Assert.That(hours[1]!["pages"]!.GetValue<long>(), Is.EqualTo(1));
    }
    [Test]
    public async Task Date_statement_no_prior_events_has_zero_opening_and_empty_usage()
    {
        await using var db = new ProvisioningDatabase(); await Seed(db);
        var p = ReportRangeTests.Period(); await Backdate(db, $"UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc='{Instant(p.FromUtc)}'");
        var report = RangeReports.Statement(JsonNode.Parse(await db.Scalar<string>(RangeSql.Generate("FWD-UNF", p)))!, p);
        Assert.That(report["openingBalance"]!.GetValue<long>(), Is.Zero);
        Assert.That(report["closingBalance"]!.GetValue<long>(), Is.EqualTo(100));
        Assert.That((JsonArray)report["dailyUsage"]!, Is.Empty);
        Assert.That((JsonArray)report["usageByHour"]!, Is.Empty);
    }
    [TestCase(false)][TestCase(true)]
    public async Task Date_reports_unknown_consumer_refuses_50018(bool documents)
    {
        await using var db = new ProvisioningDatabase(); await Seed(db);
        var error = Assert.ThrowsAsync<SqlException>(async () => await db.Execute(RangeSql.Generate("UNKNOWN", ReportRangeTests.Period(), documents)));
        Assert.That(error!.Number, Is.EqualTo(50018));
    }
    [Test]
    public async Task Date_movements_cap_preserves_full_summary_and_identifier_order()
    {
        await using var db = new ProvisioningDatabase(); await Seed(db); var p = ReportRangeTests.Period();
        // The INSTEAD OF INSERT trigger accepts one audited row at a time and stamps the time itself; bypass it for this bulk fixture.
        await Backdate(db, $"""
            UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc='{Instant(p.FromUtc.AddDays(-1))}';
            DISABLE TRIGGER dbo.TR_Allowance_Insert ON dbo.PageAllowanceGrants;
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId,BalanceAfter,RecordedAtUtc)
            SELECT TOP (1001) (SELECT ConsumerId FROM dbo.Consumers WHERE Code='FWD-UNF'),1,'Adjustment','PRIVATE_OPERATOR','PRIVATE_NOTE',NEWID(),0,'{Instant(p.FromUtc)}'
            FROM sys.all_objects a CROSS JOIN sys.all_objects b;
            ENABLE TRIGGER dbo.TR_Allowance_Insert ON dbo.PageAllowanceGrants
            """);
        var report = RangeReports.Statement(JsonNode.Parse(await db.Scalar<string>(RangeSql.Generate("FWD-UNF", p)))!, p);
        Assert.That(((JsonArray)report["movements"]!).Count, Is.EqualTo(1000));
        Assert.That(report["hasMoreMovements"]!.GetValue<bool>(), Is.True);
        Assert.That(report["adjustmentPages"]!.GetValue<long>(), Is.EqualTo(1001));
        Assert.That(report["closingBalance"]!.GetValue<long>(), Is.EqualTo(1101));
    }
}
