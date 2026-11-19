using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public static class AzureCostReports
{
    public const string Definitions = "Charged cohort: customer charges recorded in the UTC range; includes ALL Azure ledger rows of the same ConsumerId + JobId + RunNo regardless of ledger time. Uncharged: Azure usage without a recorded customer charge (failed/cancelled/unfinished runs), occurred in the range and has no charge at report time. Reversal rows are summed with their sign. Frozen cost uses the price on each usage date; recovered estimate re-prices unpriced rows by their own date. Unresolved pages have no price in force. Cost per charged page divides aggregate charged estimated cost by aggregate charged pages; zero pages gives '-'. * Incomplete: unresolved pages are excluded from estimated cost.";
    public static string Sql(string consumer, ReportPeriod period)
    {
        if (consumer.Length > 0) PageSql.Validate(new PageRequest("Status", consumer));
        return SqlResources.Expand("AzureCostRange.sql", new()
        {
            ["$consumer"] = consumer,
            ["$fromUtc"] = period.FromUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            ["$toExclusiveUtc"] = period.ToExclusiveUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)
        });
    }
    public static JsonNode Prepare(JsonNode input, ReportPeriod period, TimeProvider? clock = null)
    {
        var report = Reports.Normalize(input, false);
        var status = new AzurePriceStatus(report["priceCheck"] ?? new JsonObject(), clock);
        report["priceCheckWarnings"] = new JsonArray(status.Warnings().Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        report["internalWarning"] = Reports.InternalWarning;
        report["period"] = period.LocalLine; report["utcPeriod"] = period.UtcLine;
        report["definitions"] = Definitions;
        foreach (var key in new[] { "consumers", "pricesUsed", "currentPriceList", "scheduledPrices" }) report[key] ??= new JsonArray();
        return report;
    }
    public static string Csv(JsonNode report)
    {
        var total = (JsonObject)report["totals"]!.DeepClone(); total["consumerCode"] = "TOTAL"; total["consumerName"] = "";
        var rows = ((JsonArray)report["consumers"]!).OfType<JsonObject>().Append(total).ToArray();
        var metadata = new[] { "internalWarning", "costNotice", "period", "utcPeriod", "definitions", "priceCheckWarnings" };
        var keys = new[] { "consumerCode", "consumerName" }.Concat(total.Select(x => x.Key).Where(x => x is not ("consumerCode" or "consumerName"))).Concat(metadata).ToArray();
        // Numbers (including negative reversal sums) stay numeric for spreadsheets; only text gets the formula guard.
        string Cell(string text) => Reports.CsvCell(!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _) && (text.StartsWith('-') || text.StartsWith('\n')) ? "'" + text : text);
        return string.Join(',', keys.Select(Cell)) + "\r\n" + string.Concat(rows.Select(r => string.Join(',', keys.Select(k => Cell(k == "priceCheckWarnings" ? string.Join(" | ", ((JsonArray)report[k]!).Select(x => x!.ToString())) : (metadata.Contains(k) ? report[k] : r[k])?.ToString() ?? ""))) + "\r\n"));
    }
    public static async Task<int> Run(OperatorArguments a, IProvisionConsole console, bool? trust)
    {
        var period = ReportPeriod.Parse(a.Required("from"), a.Required("to"), a.Required("time-zone"));
        var query = Sql(a.Get("consumer"), period);
        var server = a.Has("sql-server") ? a.Get("sql-server") : CliPrompts.Ask(console, "SQL Server");
        var database = a.Has("database") ? a.Get("database") : CliPrompts.Ask(console, "Database", "ISTDocAi");
        SqlRunner.ValidateNames(server, database);
        var trusted = trust ?? (a.Has("trust-server-certificate") || CliPrompts.Ask(console, "TrustServerCertificate? yes/no", "no").Equals("yes", StringComparison.OrdinalIgnoreCase));
        await using var sql = new SqlRunner(server, database, trusted);
        var sets = await sql.Execute(query);
        var json = sets.SelectMany(x => x).SelectMany(x => x.Values).Single()?.ToString() ?? throw new ArgumentException("Empty Azure cost report.");
        var input = JsonNode.Parse(json)!;
        try { input["priceCheck"] = (await AzurePriceStatus.Read(server, database, trusted)).ToJson(); }
        catch (Microsoft.Data.SqlClient.SqlException) { input["priceCheck"] = new JsonObject { ["historyReadError"] = true }; }
        var report = Prepare(input, period);
        OperatorCli.Warning(console, Reports.InternalWarning);
        OperatorCli.Warning(console, report["costNotice"]!.ToString());
        foreach (var warning in (JsonArray)report["priceCheckWarnings"]!) OperatorCli.Warning(console, warning!.ToString());
        console.WriteLine(period.LocalLine); console.WriteLine(period.UtcLine);
        if (report["totals"]!["chargedUnresolvedPages"]!.GetValue<long>() != 0 || report["totals"]!["unchargedUnresolvedPages"]!.GetValue<long>() != 0)
            OperatorCli.Warning(console, "WARNING: Unresolved Azure pages; cost per charged page is incomplete (*).");
        if (a.Has("pdf") || a.Has("csv"))
        {
            var pdf = a.Has("pdf");
            var bytes = pdf ? Reports.Pdf(report, null, false, "AzureCost", "", a.Get("consumer", "all")) : new UTF8Encoding(false).GetBytes(Csv(report));
            using var file = new FileStream(a.Get(pdf ? "pdf" : "csv"), a.Has("force") ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            file.Write(bytes);
        }
        else console.WriteLine(report.ToJsonString());
        return 0;
    }
}
