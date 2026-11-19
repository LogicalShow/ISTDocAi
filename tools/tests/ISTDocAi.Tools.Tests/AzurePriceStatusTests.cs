using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using NUnit.Framework;
using UglyToad.PdfPig;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class AzurePriceStatusTests
{
    internal sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-16T10:00:00Z");
    internal static JsonNode Fixture() => JsonNode.Parse("""
        {"historyAvailable":true,"checks":[],"currentPrices":[{"meterCode":"DI_CUSTOM_EXTRACT","pricePer1000":30,"effectiveFrom":"2026-10-01","source":"fixture"}],"scheduledPrices":[]}
        """)!;
    internal static void Pair(JsonNode data, string outcome, DateTimeOffset at, string reason = "PRICE_SOURCE_FAILED")
    {
        var id = Guid.NewGuid().ToString();
        foreach (var meter in AzurePriceStatus.Meters) ((JsonArray)data["checks"]!).Add(new JsonObject {
            ["checkId"]=id,["meterCode"]=meter,["checkedAtUtc"]=at.ToString("O"),["outcome"]=outcome,["reasonCode"]=reason,
            ["currentPrice"]=30m,["observedPrice"]=35m,["proposedEffectiveFrom"]="2026-11-17" });
    }
    [Test]
    public void Warning_run_since_resolution_latest_change_and_rendering()
    {
        var data=Fixture(); Pair(data,"Updated",Now.AddHours(-6)); Pair(data,"Refused",Now.AddHours(-4)); Pair(data,"Failed",Now);
        var status=new AzurePriceStatus(data,new Clock(Now));
        Assert.That(status.Warnings(), Has.Length.EqualTo(2));
        Assert.That(status.Banner(), Does.Contain("since 2026-11-16 06:00 UTC").And.Contain("3) IST reports -> 4) Azure prices"));
        Assert.That(status.Render(), Does.Contain("30.0000 -> 35.0000").And.Contain("source: fixture").And.Contain("Scheduled: (none)").And.Contain("network/firewall/proxy"));
        Assert.That(status.ToJson()["meters"]![0]!["openWarning"]!["since"], Is.Not.Null);
        Pair(data,"NoChange",Now.AddMinutes(1));
        Assert.That(new AzurePriceStatus(data,new Clock(Now)).Warnings(), Is.Empty);
        Pair(data,"Refused",Now.AddMinutes(2),"NEW_REASON");
        Assert.That(new AzurePriceStatus(data,new Clock(Now)).Warnings()[0], Does.Contain("NEW_REASON").And.Contain("10:02 UTC"));
    }
    [TestCase(26,false)][TestCase(26.0001,true)][TestCase(25,false)]
    public void Stale_boundary(double hours,bool expected)
    {
        var data=Fixture(); Pair(data,"NoChange",Now.AddHours(-hours));
        var s=new AzurePriceStatus(data,new Clock(Now));
        Assert.That(s.Stale,Is.EqualTo(expected)); Assert.That(s.Banner().Length>0,Is.EqualTo(expected));
    }
    [Test]
    public void Missing_empty_permission_and_incomplete_history()
    {
        var data=Fixture(); var s=new AzurePriceStatus(data,new Clock(Now));
        Assert.That(s.NeverRan,Is.True); Assert.That(s.Banner(),Is.Empty); Assert.That(s.Render(),Does.Contain("never ran"));
        Pair(data,"NoChange",Now.AddDays(-5)); ((JsonArray)data["checks"]!).RemoveAt(1);
        Assert.That(new AzurePriceStatus(data,new Clock(Now)).LastCompletedCheckAtUtc,Is.Null);
        data["historyAvailable"]=false;
        s=new(data,new Clock(Now)); Assert.That(s.NeverRan,Is.False); Assert.That(s.Banner(),Is.Empty);
        Assert.That(s.Render(),Does.Contain("history not available (service not updated yet)"));
        data["historyReadError"]=true;
        Assert.That(new AzurePriceStatus(data).Render(),Does.Contain("check SELECT permission"));
    }
    [TestCase("--generate-sql","folder")][TestCase("--unknown","value")][TestCase("--consumer","TEST")]
    public void Cli_refuses(params string[] extra) => Assert.Throws<ArgumentException>(()=>OperatorArguments.Parse(new[]{"prices","show"}.Concat(extra).ToArray()));
    [Test]
    public void Cli_accepts_read_options() => Assert.That(OperatorArguments.Parse(["prices","show","--json","--sql-server","SQL01","--database","ISTDocAi","--trust-server-certificate"]).Has("json"),Is.True);
    [TestCase(true)][TestCase(false)]
    public void Cost_outputs_with_and_without_warnings(bool warnings)
    {
        var input=AzureCostTests.Fixture(); var price=Fixture(); if(warnings) Pair(price,"Refused",Now);
        input["priceCheck"]=price;
        var report=AzureCostReports.Prepare(input,ReportRangeTests.Period(),new Clock(Now));
        Assert.That(((JsonArray)report["priceCheckWarnings"]!).Count,Is.EqualTo(warnings?2:0));
        var csv=AzureCostReports.Csv(report); Assert.That(csv,Does.Contain("priceCheckWarnings"));
        Assert.That(csv.Contains("prices NOT updated"),Is.EqualTo(warnings));
        using var pdf=PdfDocument.Open(ISTDocAi.OperatorTool.Reports.Pdf(report,null,false,"AzureCost","","all"));
        var text=string.Join(" ",pdf.GetPages().Select(x=>x.Text));
        Assert.That(text.Contains("pricesNOTupdated"),Is.EqualTo(warnings));
    }
}
