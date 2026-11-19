using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using ISTDocAi.ReportPdf;
using NUnit.Framework;
using UglyToad.PdfPig;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class AzureCostTests
{
    private static string[] Args(params string[] extra) => new[] { "report", "azure-cost", "--from", "2026-01-01", "--to", "2026-01-31", "--time-zone", "egypt" }.Concat(extra).ToArray();
    [TestCase("--audience", "ist")][TestCase("--generate-sql", "file")][TestCase("--grant-id", "1")]
    [TestCase("--pdf", "a.pdf", "--csv", "b.csv")][TestCase("--json", "--pdf", "a.pdf")]
    [TestCase("--consumer", "bad'code")]
    public void Refuses_invalid_options(params string[] extra) => Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(Args(extra)));
    [TestCase("bad", "2026-01-31", "egypt")][TestCase("2026-02-01", "2026-01-31", "egypt")][TestCase("2026-01-01", "2026-01-31", "utc")]
    public void Refuses_invalid_period(string from, string to, string zone) => Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(["report", "azure-cost", "--from", from, "--to", to, "--time-zone", zone]));
    [Test]
    public void Missing_zone_refused_and_all_consumers_default()
    {
        Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(Args()[..^2]));
        Assert.That(OperatorArguments.Parse(Args()).Has("consumer"), Is.False);
        Assert.That(AzureCostReports.Sql("", ReportRangeTests.Period()), Does.Contain("N''<>N''"));
    }
    internal static JsonNode Fixture() => JsonNode.Parse("""
        {"consumers":[{"consumerCode":"=DANGER","consumerName":"Example","chargedRuns":1,"chargedPages":10,"chargedAzureClassifyPages":10,"chargedAzureExtractPages":10,"chargedFrozenCostUsd":1,"chargedRecoveredEstimateUsd":0,"chargedUnresolvedPages":1,"chargedEstimatedCostUsd":1,"costPerChargedPageUsd":0.1,"unchargedEstimatedCostUsd":0,"unchargedUnresolvedPages":0},
        {"consumerCode":"ZERO","chargedRuns":0,"chargedPages":0,"chargedEstimatedCostUsd":0,"costPerChargedPageUsd":null,"chargedUnresolvedPages":0}],
        "totals":{"chargedRuns":1,"chargedPages":10,"chargedEstimatedCostUsd":1,"costPerChargedPageUsd":0.1,"chargedUnresolvedPages":1,"unchargedUnresolvedPages":0},
        "currentPriceList":[{"meterCode":"DI_CUSTOM_EXTRACT","pricePer1000":30,"effectiveFrom":"2026-01-01","source":"Fixture source"}],"pricesUsed":[{"meterCode":"DI_CUSTOM_EXTRACT","pricePer1000":30,"effectiveFrom":"2026-01-01","source":"Fixture source"}],"costNotice":"ESTIMATE; Azure invoice is authoritative"}
        """)!;
    [Test]
    public void Csv_is_clean_protects_formula_and_has_aggregate_total()
    {
        var report = AzureCostReports.Prepare(Fixture(), ReportRangeTests.Period());
        Assert.That(report["internalWarning"]!.ToString(), Is.EqualTo(ISTDocAi.OperatorTool.Reports.InternalWarning));
        var csv = AzureCostReports.Csv(report);
        Assert.That(csv, Does.StartWith("\"consumerCode\"")); Assert.That(csv, Does.Contain("'=DANGER"));
        Assert.That(csv.Split("\r\n")[^2], Does.StartWith("\"TOTAL\""));
        Assert.That(report["totals"]!["costPerChargedPageUsd"]!.GetValue<decimal>(), Is.EqualTo(.1m));
    }
    [Test]
    public void Csv_keeps_negative_numbers_numeric_and_guards_dash_text()
    {
        var input = Fixture();
        input["consumers"]![0]!["chargedEstimatedCostUsd"] = -1.5m;
        input["consumers"]![0]!["consumerName"] = "-cmd";
        var csv = AzureCostReports.Csv(AzureCostReports.Prepare(input, ReportRangeTests.Period()));
        Assert.That(csv, Does.Contain("\"-1.5\"").And.Not.Contain("'-1.5"));
        Assert.That(csv, Does.Contain("\"'-cmd\""));
    }
    [Test]
    public void Customer_audience_refused()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => ReportRenderer.Render("{}", "Customer", "AzureCost", stream));
    }
    [Test]
    public void Pdf_contains_prices_null_and_incomplete_marker()
    {
        var report = AzureCostReports.Prepare(Fixture(), ReportRangeTests.Period());
        var bytes = ISTDocAi.OperatorTool.Reports.Pdf(report, null, false, "AzureCost", "", "all");
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Join(" ", pdf.GetPages().Select(x => x.Text));
        Assert.That(text, Does.Contain("0.100000*")); Assert.That(text, Does.Contain("-"));
        Assert.That(text, Does.Contain("CurrentAzurepricelist")); Assert.That(text, Does.Contain("Pricesusedinthisperiod"));
        Assert.That(text, Does.Contain("donotsendtothecustomer"));
    }
}
