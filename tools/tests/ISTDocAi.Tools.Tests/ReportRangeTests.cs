using System.Globalization;
using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using NUnit.Framework;
using UglyToad.PdfPig;
using OperatorReports = ISTDocAi.OperatorTool.Reports;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class ReportRangeTests
{
    internal static ReportPeriod Period(string zone = "egypt") => ReportPeriod.Parse("2026-11-01", "2026-11-15", zone);
    internal static JsonNode Fixture() => JsonNode.Parse("""
        {"consumerName":"Azure USD $ price cost Company","consumerCode":"TEST","openingBalance":100,"topUpPages":20,"adjustmentPages":-3,"reversalPages":-10,"chargedPages":7,"chargedRuns":2,"closingBalance":100,"independentClosingBalance":100,
        "note":"PRIVATE_NOTE","operatorIdentity":"PRIVATE_OPERATOR","ledgerCost":123,"hasMoreMovements":true,
        "movements":[{"recordedAtUtc":"2026-11-02T00:00:00Z","kind":"Reversal","pages":-10,"originalRecordedAtUtc":"2026-10-29T00:00:00Z","originalPages":10,"note":"PRIVATE_NOTE","operatorIdentity":"PRIVATE_OPERATOR"}],
        "usageByHour":[{"hourUtc":"2026-10-31T22:00:00Z","runs":1,"pages":3},{"hourUtc":"2026-11-01T00:00:00Z","runs":1,"pages":4}]}
        """)!;
    private static readonly string[] Base = ["pages", "statement", "--consumer", "TEST", "--from", "2026-11-01", "--to", "2026-11-15", "--time-zone", "egypt"];
    [TestCase("egypt", "2026-01-01", 2)][TestCase("egypt", "2026-07-01", 3)]
    [TestCase("saudi", "2026-01-01", 3)][TestCase("saudi", "2026-07-01", 3)]
    public void Local_midnights_use_zone_offset(string zone, string date, int offset)
    {
        var p = ReportPeriod.Parse(date, date, zone);
        Assert.That(p.FromUtc, Is.EqualTo(DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddHours(-offset)));
        Assert.That(p.ToExclusiveUtc - p.FromUtc, Is.EqualTo(TimeSpan.FromHours(24)));
    }
    internal static DateTime Transition(int year, TimeZoneInfo.TransitionTime t)
    {
        if (t.IsFixedDateRule) return new DateTime(year, t.Month, t.Day).Add(t.TimeOfDay.TimeOfDay);
        var first = new DateTime(year, t.Month, 1);
        var day = 1 + ((int)t.DayOfWeek - (int)first.DayOfWeek + 7) % 7 + (t.Week - 1) * 7;
        if (day > DateTime.DaysInMonth(year, t.Month)) day -= 7;
        return new DateTime(year, t.Month, day).Add(t.TimeOfDay.TimeOfDay);
    }
    [Test]
    public void Egypt_2026_transitions_from_adjustment_rules_tile_and_fold()
    {
        var zone = ReportPeriod.Resolve("egypt");
        var rule = zone.GetAdjustmentRules().Single(r => r.DateStart <= new DateTime(2026, 7, 1) && r.DateEnd >= new DateTime(2026, 7, 1));
        var start = Transition(2026, rule.DaylightTransitionStart);
        var end = Transition(2026, rule.DaylightTransitionEnd);
        Assert.That(zone.IsInvalidTime(start), Is.True);
        Assert.That(ReportPeriod.Boundary(start, zone), Is.EqualTo(TimeZoneInfo.ConvertTimeToUtc(start + rule.DaylightDelta, zone)));
        var ambiguous = end - rule.DaylightDelta;
        Assert.That(zone.IsAmbiguousTime(ambiguous), Is.True);
        Assert.That(ReportPeriod.Boundary(ambiguous, zone), Is.EqualTo(new DateTime(ambiguous.Ticks - zone.GetAmbiguousTimeOffsets(ambiguous).Max().Ticks, DateTimeKind.Utc)));
        foreach (var (day, hours) in new[] { (start.Date, 23), (ambiguous.Date, 25) })
        {
            string Date(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var p = ReportPeriod.Parse(Date(day), Date(day), "egypt");
            var next = ReportPeriod.Parse(Date(day.AddDays(1)), Date(day.AddDays(1)), "egypt");
            Assert.That(p.ToExclusiveUtc, Is.EqualTo(next.FromUtc));
            Assert.That(p.ToExclusiveUtc - p.FromUtc, Is.EqualTo(TimeSpan.FromHours(hours)));
            var fixture = Fixture(); var buckets = new JsonArray();
            for (var utc = p.FromUtc; utc < p.ToExclusiveUtc; utc = utc.AddHours(1)) buckets.Add(JsonNode.Parse($"{{\"hourUtc\":\"{utc:O}\",\"runs\":1,\"pages\":2}}"));
            fixture["usageByHour"] = buckets;
            var daily = (JsonArray)RangeReports.Statement(fixture, p)["dailyUsage"]!;
            Assert.That(daily.Count, Is.EqualTo(1));
            Assert.That(daily[0]!["runs"]!.GetValue<long>(), Is.EqualTo(hours));
            Assert.That(daily[0]!["pages"]!.GetValue<long>(), Is.EqualTo(hours * 2));
        }
    }
    [TestCase("2026-1-01", "2026-01-02")][TestCase("2026-02-30", "2026-03-01")]
    [TestCase("2026-02-02", "2026-02-01")][TestCase("2000-01-01", "2026-01-01")]
    [TestCase("9999-12-31", "9999-12-31")]
    public void Invalid_periods_refused(string from, string to) => Assert.Throws<ArgumentException>(() => ReportPeriod.Parse(from, to, "egypt"));
    [Test]
    public void Inclusive_span_limit_and_future_dates()
    {
        var start = new DateOnly(2030, 1, 1);
        string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.DoesNotThrow(() => ReportPeriod.Parse(Date(start), Date(start.AddDays(3659)), "saudi"));
        Assert.Throws<ArgumentException>(() => ReportPeriod.Parse(Date(start), Date(start.AddDays(3660)), "saudi"));
    }
    [TestCase("--generate-sql", "folder")][TestCase("--grant-id", "1")][TestCase("--audience", "customer")]
    [TestCase("--csv", "file.csv")]
    public void Statement_refuses_foreign_options(string option, string value) => Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(Base.Concat([option, value]).ToArray()));
    [Test]
    public void Required_zone_dates_and_exclusive_output_validated_at_parse()
    {
        Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(Base[..^2]));
        foreach (var key in new[] { "--from", "--to", "--time-zone", "--consumer" })
        {
            var invalid = Base.ToArray(); invalid[Array.IndexOf(invalid, key) + 1] = "bad'";
            Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(invalid));
        }
        Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(Base.Concat(["--json", "--pdf", "file.pdf"]).ToArray()));
        var documents = Base.ToArray(); documents[1] = "documents-by-date";
        foreach (var pair in new[] { new[] { "--pdf", "file.pdf" }, new[] { "--company-name", "Name" }, new[] { "--audience", "customer" }, new[] { "--grant-id", "1" }, new[] { "--generate-sql", "folder" } })
            Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(documents.Concat(pair).ToArray()));
    }
    [Test]
    public void Statement_reconciles_folds_and_sanitizes_JSON_and_PDF()
    {
        var report = RangeReports.Statement(Fixture(), Period());
        Assert.That(((JsonArray)report["dailyUsage"]!).Count, Is.EqualTo(1));
        Assert.That(report["dailyUsage"]![0]!["pages"]!.GetValue<long>(), Is.EqualTo(7));
        Assert.That(report["period"]!.ToString(), Is.EqualTo("Period: 2026-11-01 to 2026-11-15 (Egypt time)"));
        Assert.That(report["utcPeriod"]!.ToString(), Is.EqualTo("UTC: 2026-10-31 22:00 to 2026-11-15 22:00 (end exclusive)"));
        Safe(report.ToJsonString());
        using var pdf = PdfDocument.Open(OperatorReports.Pdf(report, null, true, "StatementRange", "Azure USD $ cost price", "TEST"));
        var text = string.Join(" ", pdf.GetPages().Select(x => x.Text)); Safe(text);
        Assert.That(text, Does.Contain("Dailyusage").And.Contain("first1000movements").And.Contain("Openingbalance"));
        var bad = Fixture(); bad["independentClosingBalance"] = 99L;
        Assert.That(Assert.Throws<ArgumentException>(() => RangeReports.Statement(bad, Period()))!.Message, Is.EqualTo("Statement does not reconcile; no file written."));
    }
    [Test]
    public void Range_fields_are_allowed_only_for_range_reports()
    {
        var node = JsonNode.Parse("""{"chargeId":1,"kind":"TopUp","timeZone":"egypt","openingBalance":5}""")!;
        var plain = OperatorReports.Normalize(node, true).AsObject();
        Assert.That(plain.ContainsKey("chargeId"), Is.True);
        Assert.That(plain.ContainsKey("kind") || plain.ContainsKey("timeZone") || plain.ContainsKey("openingBalance"), Is.False);
        var range = OperatorReports.Normalize(node, true, range: true).AsObject();
        Assert.That(range.ContainsKey("kind") && range.ContainsKey("timeZone") && range.ContainsKey("openingBalance"), Is.True);
    }
    private static void Safe(string output) => Assert.That(output, Does.Not.Match("(?i)USD|\\$|price|cost|Azure|PRIVATE_NOTE|PRIVATE_OPERATOR|operatorIdentity|\\\"note\\\""));
    internal static Dictionary<string, object?> Row(string reason = "New") => new()
    {
        ["recordedAtUtc"] = new DateTime(2026, 11, 1, 0, 0, 0), ["pages"] = 2, ["runNo"] = 1,
        ["jobId"] = Guid.Empty, ["resolvedDocumentTypeCode"] = "BL", ["runReason"] = reason,
        ["note"] = "PRIVATE_NOTE", ["operatorIdentity"] = "PRIVATE_OPERATOR", ["azureCostUsd"] = 9
    };
    [TestCase("=1+1")][TestCase("+SUM(1)")][TestCase("-1+2")][TestCase("@SUM(1)")][TestCase("\tformula")][TestCase("\rformula")]
    public void CSV_formula_protection(string reason) => Assert.That(RangeReports.CsvRow(RangeReports.DocumentRow(Row(reason)), Period()), Does.Contain("\"'" + reason));
    [Test]
    public async Task CSV_streams_sanitized_rows_and_failure_leaves_no_partial_file()
    {
        var folder = ModelVerdictTests.Folder(); Directory.CreateDirectory(folder); var path = Path.Combine(folder, "docs.csv");
        try
        {
            var count = await RangeReports.CsvFile(path, false, Period(), async callback => { await callback(Row("Azure USD cost price $")); await callback(Row()); });
            Assert.That(count, Is.EqualTo(2)); var text = File.ReadAllText(path); Safe(text);
            Assert.That(text, Does.StartWith("\"Time (Egypt)\",\"Time (UTC)\""));
            foreach (var exists in new[] { true, false })
            {
                if (!exists) File.Delete(path);
                Assert.ThrowsAsync<IOException>(async () => await RangeReports.CsvFile(path, true, Period(), async callback => { await callback(Row()); throw new IOException("fixture failure"); }));
                Assert.That(File.Exists(path), Is.EqualTo(exists));
                if (exists) Assert.That(File.ReadAllText(path), Is.EqualTo(text));
                Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
            }
        }
        finally { Directory.Delete(folder, true); }
    }
    [Test]
    public async Task Documents_JSON_cap_refuses_instead_of_truncating()
    {
        var report = await RangeReports.Documents(Period(), callback => callback(Row("Azure USD cost price $"))); Safe(report.ToJsonString());
        Assert.ThrowsAsync<ArgumentException>(async () => await RangeReports.Documents(Period(), async callback => { for (var i = 0; i <= 10000; i++) await callback(Row()); }));
    }
}
