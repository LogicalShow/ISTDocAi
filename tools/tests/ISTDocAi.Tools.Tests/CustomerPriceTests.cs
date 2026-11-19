using System.Globalization;
using ISTDocAi.OperatorTool;
using ISTDocAi.ReportPdf;
using NUnit.Framework;
using UglyToad.PdfPig;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class CustomerPriceTests
{
    private static ReportPeriod Period => ReportRangeTests.Period();
    private static Dictionary<string, object?> Row(long pages = 3, DateTime? utc = null, string type = "BL")
    {
        var row = ReportRangeTests.Row(); row["pages"] = pages; row["resolvedDocumentTypeCode"] = type;
        if (utc.HasValue) row["recordedAtUtc"] = utc.Value;
        return row;
    }
    private static Task Read(Func<Dictionary<string, object?>, Task> callback) => callback(Row());
    [TestCase("0.5", "0.50")][TestCase("1", "1.00")][TestCase("12.3456", "12.3456")][TestCase("100000", "100000.00")][TestCase("1.2300", "1.23")]
    public void Valid_price(string input, string display) => Assert.That(CustomerPriceReports.Rate(CustomerPriceReports.Price(input)), Is.EqualTo(display));
    [TestCase("-1")][TestCase("0")][TestCase("1,000")][TestCase("1e3")][TestCase("12.34567")][TestCase("100000.01")][TestCase("+1")][TestCase(" 1")][TestCase("")]
    public void Invalid_price(string value) => Assert.Throws<ArgumentException>(() => CustomerPriceReports.Price(value));
    [TestCase("egp", "EGP")][TestCase("usd", "USD")][TestCase("SAR", "SAR")]
    public void Valid_currency(string input, string expected) => Assert.That(CustomerPriceReports.Currency(input), Is.EqualTo(expected));
    [TestCase("EUR")][TestCase("")]
    public void Invalid_currency(string value) => Assert.Throws<ArgumentException>(() => CustomerPriceReports.Currency(value));
    [Test]
    public void CLI_required_options_and_exclusivity()
    {
        string[] args = ["report", "customer-price", "--consumer", "TEST", "--from", "2026-11-01", "--to", "2026-11-15", "--time-zone", "egypt", "--price-per-page", "0.5", "--currency", "usd"];
        Assert.That(OperatorArguments.Parse(args).Get("currency"), Is.EqualTo("USD"));
        foreach (var key in new[] { "consumer", "from", "to", "time-zone", "price-per-page", "currency" })
        {
            var list = args.ToList(); var index = list.IndexOf("--" + key); list.RemoveRange(index, 2);
            Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(list.ToArray()));
        }
        foreach (var pair in new[] { new[] { "--audience", "customer" }, new[] { "--grant-id", "1" }, new[] { "--generate-sql", "folder" }, new[] { "--pdf", "a", "--csv", "b" } })
            Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(args.Concat(pair).ToArray()));
    }
    [Test]
    public async Task Per_line_rounding_reconciles_daily_and_total_and_detects_tampering()
    {
        var r = await CustomerPriceReports.Collect(Period, .335m, "EGP", async c => { await c(Row()); await c(Row()); });
        Assert.That(r.Lines.Select(x => x.Amount), Is.EqualTo(new[] { 1.01m, 1.01m }));
        Assert.That(r.Amount, Is.EqualTo(2.02m)); Assert.That(r.Pages, Is.EqualTo(6)); Assert.That(r.Documents, Is.EqualTo(2));
        Assert.That(r.Daily.Single().Amount, Is.EqualTo(r.Amount));
        Assert.That(Assert.Throws<ArgumentException>(() => CustomerPriceReports.Reconcile(r with { Amount = 2.01m }))!.Message, Is.EqualTo("Priced statement does not reconcile; no file written."));
    }
    [Test]
    public async Task DST_days_fold_all_UTC_hours_into_one_local_day()
    {
        var zone = ReportPeriod.Resolve("egypt"); var rule = zone.GetAdjustmentRules().Single(r => r.DateStart <= new DateTime(2026,7,1) && r.DateEnd >= new DateTime(2026,7,1));
        foreach (var day in new[] { ReportRangeTests.Transition(2026, rule.DaylightTransitionStart).Date, (ReportRangeTests.Transition(2026, rule.DaylightTransitionEnd) - rule.DaylightDelta).Date })
        {
            var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); var p = ReportPeriod.Parse(date, date, "egypt");
            var r = await CustomerPriceReports.Collect(p, .335m, "SAR", async c => { for (var at = p.FromUtc; at < p.ToExclusiveUtc; at = at.AddHours(1)) await c(Row(3, at)); });
            Assert.That(r.Daily.Count, Is.EqualTo(1)); Assert.That(r.Daily.Single().Documents, Is.EqualTo((long)(p.ToExclusiveUtc-p.FromUtc).TotalHours));
            Assert.That(r.Daily.Single().Amount, Is.EqualTo(r.Amount));
        }
    }
    [TestCase("EGP")][TestCase("USD")][TestCase("SAR")]
    public async Task PDF_has_customer_rate_totals_subtitle_and_no_private_content(string currency)
    {
        var r = await CustomerPriceReports.Collect(Period, .335m, currency, c => c(Row(type: "Azure meter ledger PRIVATE_NOTE PRIVATE_OPERATOR")));
        using var pdf = PdfDocument.Open(CustomerPriceReports.Pdf(r, "Azure USD $ cost ledger PRIVATE_OPERATOR", "TEST"));
        var text = string.Join(" ", pdf.GetPages().Select(x => x.Text));
        Assert.That(text, Does.Contain("Pricedpageusagestatement").And.Contain("Rate:0.335" + currency + "perpage").And.Contain("1.01" + currency).And.Contain("notataxinvoice."));
        Assert.That(text, Does.Not.Match("(?i)Azure|cost|meter|ledger|PRIVATE_NOTE|PRIVATE_OPERATOR"));
        if (currency == "EGP") Assert.That(text, Does.Not.Match("USD|\\$"));
        using var output = new MemoryStream(); Assert.Throws<ArgumentException>(() => ReportRenderer.Render("{}", "Ist", "PriceRange", output));
    }
    [Test]
    public async Task PDF_buffer_bound_JSON_cap_and_empty_set()
    {
        async Task Many(Func<Dictionary<string, object?>, Task> c) { for (var i = 0; i < 10001; i++) await c(Row()); }
        var report = await CustomerPriceReports.Collect(Period, 1, "EGP", Many, 1000, false);
        Assert.That(report.Lines.Count, Is.EqualTo(1000)); Assert.That(report.Documents, Is.EqualTo(10001));
        Assert.That(Assert.ThrowsAsync<ArgumentException>(() => CustomerPriceReports.Collect(Period, 1, "EGP", Many))!.Message, Is.EqualTo("More than 10000 documents; use --csv."));
        var empty = await CustomerPriceReports.Collect(Period, 1, "EGP", _ => Task.CompletedTask); Assert.That(empty.Amount, Is.Zero);
    }
    [TestCase("=SUM(1)")][TestCase("-formula")][TestCase("+formula")][TestCase("@formula")]
    public async Task CSV_columns_numeric_cells_TOTAL_and_atomic_cleanup(string type)
    {
        var folder = ModelVerdictTests.Folder(); Directory.CreateDirectory(folder); var path = Path.Combine(folder,"priced.csv");
        try
        {
            var report = await CustomerPriceReports.CsvFile(path, false, Period, .335m, "EGP", c => c(Row(type: type)));
            Assert.That(report.Lines, Is.Empty);
            var text = File.ReadAllText(path);
            Assert.That(text, Does.StartWith("\"Time (Egypt)\",\"Time (UTC)\",\"Document type\",\"Pages\",\"Price per page\",\"Currency\",\"Amount\",\"Job id\",\"Run\""));
            Assert.That(text, Does.Contain("\"'" + type + "\",3,0.335,EGP,1.01,").And.Contain("TOTAL,,,3,,EGP,1.01,,"));
            foreach (var existing in new[] { true, false })
            {
                if (!existing) File.Delete(path);
                Assert.ThrowsAsync<IOException>(() => CustomerPriceReports.CsvFile(path, true, Period, 1, "EGP", async c => { await c(Row()); throw new IOException("fixture"); }));
                Assert.That(File.Exists(path), Is.EqualTo(existing)); if (existing) Assert.That(File.ReadAllText(path), Is.EqualTo(text));
                Assert.That(Directory.GetFiles(folder,"*.tmp"), Is.Empty);
            }
        }
        finally { Directory.Delete(folder,true); }
    }
    [Test]
    public async Task PDF_truncation_note_and_JSON_customer_projection()
    {
        var r = await CustomerPriceReports.Collect(Period, .5m, "USD", async c => { for (int i = 0; i < 1001; i++) await c(Row()); }, 1000, false);
        using var pdf = PdfDocument.Open(CustomerPriceReports.Pdf(r, "Company", "TEST"));
        Assert.That(string.Join(" ", pdf.GetPages().Select(p => p.Text)), Does.Contain("Showingfirst1000documents;fulllistintheCSVexport."));
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        Assert.That(json, Does.Contain("USD").And.Not.Match("(?i)Azure|cost|meter|ledger|PRIVATE_NOTE|PRIVATE_OPERATOR"));
    }
    [Test]
    public void SQL_reads_customer_charges_only() => Assert.That(CustomerPriceReports.Sql("TEST", Period), Does.Contain("CustomerPageCharges").And.Not.Match("(?i)UsageLedger|AzureMeterPrices"));
}
