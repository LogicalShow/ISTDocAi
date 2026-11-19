using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public static class RangeReports
{
    public const string BalanceNote = "Balances are ledger balances from recorded top-ups and charges; pages reserved by documents still processing are not included.";
    public const string DocumentsNote = "Documents = charged processing runs.";
    public static DateTime Utc(JsonNode? value) => DateTime.Parse(value?.ToString() ?? throw new ArgumentException("Missing report timestamp."), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    public static JsonNode Statement(JsonNode input, ReportPeriod period)
    {
        var report = Reports.Normalize(input, true, range: true);
        long Number(string key) => report[key]?.GetValue<long>() ?? throw new ArgumentException("Missing statement total.");
        var closing = checked(Number("openingBalance") + Number("topUpPages") + Number("adjustmentPages") + Number("reversalPages") - Number("chargedPages"));
        if (closing != Number("closingBalance") || closing != Number("independentClosingBalance")) throw new ArgumentException("Statement does not reconcile; no file written.");
        report["period"] = period.LocalLine;
        report["utcPeriod"] = period.UtcLine;
        // The renderer converts movement times with this explicit zone key (not by parsing the period text).
        report["timeZone"] = period.Zone;
        // FOR JSON yields NULL for an empty subquery; expose empty arrays so JSON consumers see a stable shape.
        report["movements"] ??= new JsonArray();
        report["usageByHour"] ??= new JsonArray();
        var daily = new SortedDictionary<string, (long Runs, long Pages)>(StringComparer.Ordinal);
        foreach (var hour in report["usageByHour"] as JsonArray ?? [])
        {
            var day = period.LocalTime(Utc(hour!["hourUtc"]))[..10];
            daily.TryGetValue(day, out var total);
            daily[day] = (checked(total.Runs + hour["runs"]!.GetValue<long>()), checked(total.Pages + hour["pages"]!.GetValue<long>()));
        }
        report["dailyUsage"] = new JsonArray(daily.Select(x => (JsonNode)new JsonObject { ["date"] = x.Key, ["runs"] = x.Value.Runs, ["pages"] = x.Value.Pages }).ToArray());
        return report;
    }
    public static JsonNode DocumentRow(Dictionary<string, object?> row) => Reports.Normalize(JsonSerializer.SerializeToNode(row)!, true, range: true);
    private static string Cell(string text) => Reports.CsvCell(text.Length > 0 && text[0] is '-' or '\n' ? "'" + text : text);
    public static string CsvHeader(ReportPeriod period) => string.Join(',', new[] { "Time (" + period.ZoneName + ")", "Time (UTC)", "Pages", "Document type", "Reason", "Job id", "Run" }.Select(Cell));
    public static string CsvRow(JsonNode input, ReportPeriod period)
    {
        var row = Reports.Normalize(input, true, range: true);
        var utc = Utc(row["recordedAtUtc"]);
        return string.Join(',', new[] { period.LocalTime(utc), utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), row["pages"]?.ToString() ?? "", row["resolvedDocumentTypeCode"]?.ToString() ?? "", row["runReason"]?.ToString() ?? "", row["jobId"]?.ToString() ?? "", row["runNo"]?.ToString() ?? "" }.Select(Cell));
    }
    // The destination is only changed after the reader and writer both finish successfully.
    public static async Task<long> CsvFile(string path, bool force, ReportPeriod period, Func<Func<Dictionary<string, object?>, Task>, Task> read)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(full) && !force) throw new ArgumentException("Existing files require --force.");
        var temporary = Path.Combine(Path.GetDirectoryName(full)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
        long count = 0;
        try
        {
            await using (var writer = new StreamWriter(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false)))
            {
                writer.NewLine = "\r\n";
                await writer.WriteLineAsync(CsvHeader(period));
                await read(async row => { await writer.WriteLineAsync(CsvRow(DocumentRow(row), period)); count++; });
            }
            File.Move(temporary, full, force);
            return count;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task<JsonNode> Documents(ReportPeriod period, Func<Func<Dictionary<string, object?>, Task>, Task> read)
    {
        var rows = new JsonArray();
        await read(row =>
        {
            if (rows.Count == 10000) throw new ArgumentException("More than 10000 documents; use --csv.");
            rows.Add(DocumentRow(row)); return Task.CompletedTask;
        });
        return new JsonObject { ["period"] = period.LocalLine, ["utcPeriod"] = period.UtcLine, ["timeZone"] = period.Zone, ["documents"] = rows };
    }
    public static async Task<int> Run(OperatorArguments a, IProvisionConsole console, bool? trust)
    {
        var period = ReportPeriod.Parse(a.Required("from"), a.Required("to"), a.Required("time-zone"));
        var documents = a.Action == "documents-by-date";
        var query = RangeSql.Generate(a.Required("consumer"), period, documents);
        var server = a.Has("sql-server") ? a.Get("sql-server") : CliPrompts.Ask(console, "SQL Server");
        var database = a.Has("database") ? a.Get("database") : CliPrompts.Ask(console, "Database", "ISTDocAi");
        SqlRunner.ValidateNames(server, database);
        var trusted = trust ?? (a.Has("trust-server-certificate") || CliPrompts.Ask(console, "TrustServerCertificate? yes/no", "no").Equals("yes", StringComparison.OrdinalIgnoreCase));
        await using var sql = new SqlRunner(server, database, trusted);
        if (documents && a.Has("csv"))
        {
            var count = await CsvFile(a.Get("csv"), a.Has("force"), period, callback => sql.Stream(query, callback));
            console.WriteLine(period.LocalLine); console.WriteLine(period.UtcLine); console.WriteLine($"Documents: {count}");
            return 0;
        }
        JsonNode report;
        if (documents) report = await Documents(period, callback => sql.Stream(query, callback));
        else
        {
            var sets = await sql.Execute(query);
            var json = sets.SelectMany(x => x).SelectMany(x => x.Values).Single()?.ToString() ?? throw new ArgumentException("Empty statement.");
            report = Statement(JsonNode.Parse(json)!, period);
            if (report["hasMoreMovements"]?.GetValue<bool>() == true) console.WriteLine("WARNING: Showing first 1000 movements.");
        }
        if (a.Has("pdf"))
        {
            var bytes = Reports.Pdf(report, null, true, "StatementRange", a.Get("company-name", report["consumerName"]?.ToString() ?? ""), a.Required("consumer"));
            using var file = new FileStream(a.Get("pdf"), a.Has("force") ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            file.Write(bytes);
        }
        else console.WriteLine(report.ToJsonString());
        return 0;
    }
}
