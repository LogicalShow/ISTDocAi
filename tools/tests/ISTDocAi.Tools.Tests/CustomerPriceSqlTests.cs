using Dapper;
using ISTDocAi.OperatorTool;
using ISTDocAi.Tools.Tests.Provisioning;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

public sealed partial class OperatorSqlTests
{
    [Test]
    public async Task Customer_price_totals_and_inclusive_start_exclusive_end()
    {
        await using var db = new ProvisioningDatabase(); var p = ReportRangeTests.Period(); await RangeSeed(db, p);
        var query = CustomerPriceReports.Sql("FWD-UNF", p);
        Assert.That(query, Does.Not.Match("(?i)UsageLedger|AzureMeterPrices"));
        await using var connection = new SqlConnection(db.ConnectionString);
        var report = await CustomerPriceReports.Collect(p, .335m, "EGP", async callback =>
        {
            foreach (IDictionary<string, object> row in await connection.QueryAsync(query))
                await callback(row.ToDictionary(x => x.Key, x => (object?)x.Value));
        });
        Assert.That(report.Documents, Is.EqualTo(3)); Assert.That(report.Pages, Is.EqualTo(7)); Assert.That(report.Amount, Is.EqualTo(2.36m));
        Assert.That(report.Lines.Select(x => x.Pages), Is.EqualTo(new long[] { 3, 3, 1 }));
        Assert.That(report.Lines[0].Time, Is.EqualTo(p.LocalTime(p.FromUtc)));
        Assert.That(report.Daily.Sum(x => x.Amount), Is.EqualTo(report.Amount));
    }
    [Test]
    public async Task Customer_price_unknown_consumer_50018()
    {
        await using var db = new ProvisioningDatabase(); await Seed(db);
        var error = Assert.ThrowsAsync<SqlException>(() => db.Execute(CustomerPriceReports.Sql("UNKNOWN", ReportRangeTests.Period())));
        Assert.That(error!.Number, Is.EqualTo(50018));
    }
}
