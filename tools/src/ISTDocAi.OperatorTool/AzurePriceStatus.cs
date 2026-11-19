using System.Globalization;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public sealed class AzurePriceStatus
{
    public static readonly string[] Meters = ["DI_CUSTOM_EXTRACT", "DI_CUSTOM_CLASSIFY"];
    public JsonNode Data { get; }
    public bool HistoryAvailable => Data["historyAvailable"]?.GetValue<bool>() == true;
    public bool NeverRan => HistoryAvailable && Checks.Length == 0;
    private JsonNode[] Checks { get; }
    public DateTimeOffset? LastCompletedCheckAtUtc { get; }
    public bool Stale { get; }
    public AzurePriceStatus(JsonNode data, TimeProvider? clock = null)
    {
        Data = data.DeepClone();
        Checks = (Data["checks"] as JsonArray ?? []).OfType<JsonNode>().OrderBy(x => At(x)).ThenBy(x => S(x,"checkId"), StringComparer.Ordinal).ToArray();
        LastCompletedCheckAtUtc = Checks.GroupBy(x => S(x,"checkId")).Where(g => Meters.All(m => g.Any(x => S(x,"meterCode") == m))).Select(g => (DateTimeOffset?)g.Max(At)).Max();
        Stale = HistoryAvailable && LastCompletedCheckAtUtc is { } last && (clock ?? TimeProvider.System).GetUtcNow() - last > TimeSpan.FromHours(26);
    }
    private static string S(JsonNode n, string key) => n[key]?.ToString() ?? "";
    private static DateTimeOffset At(JsonNode n) => DateTimeOffset.Parse(S(n,"checkedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    private static string Date(DateTimeOffset d) => d.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    private static string Price(JsonNode n, string key) => n[key]?.GetValue<decimal>().ToString("F4", CultureInfo.InvariantCulture) ?? "-";
    public static string Reason(string code) => code switch
    {
        "PRICE_SOURCE_FAILED" => "could not read prices.azure.com (network/firewall/proxy)",
        "EXISTING_EFFECTIVE_DATE_CONFLICT" => "a different price is already set for tomorrow",
        "FUTURE_PRICE_SCHEDULE_EXISTS" => "a later price is already scheduled",
        "NO_CURRENT_BASELINE" => "no current price row",
        "METER_MISSING" => "Azure meter was not found",
        "METER_AMBIGUOUS" => "multiple Azure meters matched",
        "INVALID_METER" => "Azure meter details are invalid",
        "INVALID_PRICE" => "Azure price is invalid",
        "AZURE_PRICE_NOT_YET_EFFECTIVE" => "Azure price is not yet effective",
        _ => code
    };
    public string[] Warnings()
    {
        if (!HistoryAvailable) return [];
        var lines = new List<string>();
        foreach (var meter in Meters)
        {
            var rows = Checks.Where(x => S(x,"meterCode") == meter).ToArray();
            var run = rows.Reverse().TakeWhile(x => S(x,"outcome") is "Refused" or "Failed").ToArray();
            if (run.Length > 0) lines.Add($"Azure price check needs review since {Date(run.Min(At))}: {meter}: {Reason(S(run[0],"reasonCode"))} - prices NOT updated");
        }
        if (Stale) lines.Add($"Azure price check is not running: last completed check {Date(LastCompletedCheckAtUtc!.Value)} (over 26 hours)");
        return lines.ToArray();
    }
    public string Banner() => Warnings() is { Length: > 0 } lines ? string.Join(Environment.NewLine, lines.Select(x => "!! " + x)) + Environment.NewLine + "   See: 3) IST reports -> 4) Azure prices" : "";
    public string Render()
    {
        var lines = new List<string> { "Azure prices (USD per 1000 pages)" };
        string Row(JsonNode n) => $"{S(n,"meterCode")}   {Price(n,"pricePer1000")}  since {S(n,"effectiveFrom")}   source: {S(n,"source")}";
        foreach (var m in Meters) lines.Add((Data["currentPrices"] as JsonArray ?? []).FirstOrDefault(x => x is not null && S(x,"meterCode") == m) is { } r ? Row(r) : m + "   no current price row");
        var scheduled = (Data["scheduledPrices"] as JsonArray ?? []).OfType<JsonNode>().ToArray();
        lines.Add("Scheduled: " + (scheduled.Length == 0 ? "(none)" : string.Join(Environment.NewLine, scheduled.Select(Row))));
        var lastRows = LastCompletedCheckAtUtc is { } at ? Checks.Where(x => At(x) <= at).GroupBy(x => S(x,"checkId")).Where(g => Meters.All(m => g.Any(x => S(x,"meterCode") == m))).OrderByDescending(g => g.Max(At)).First().OrderBy(x => Array.IndexOf(Meters,S(x,"meterCode"))).Select(x => S(x,"outcome")) : [];
        lines.Add("Last price check: " + (!HistoryAvailable ? Data["historyReadError"]?.GetValue<bool>() == true ? "could not read price check history; check SELECT permission on dbo.AzurePriceChecks" : "history not available (service not updated yet)" : LastCompletedCheckAtUtc is { } last ? $"{Date(last)} ({string.Join(", ",lastRows)})" : "never ran (job disabled or not deployed)"));
        if (!HistoryAvailable && Data["historyReadError"]?.GetValue<bool>() != true) lines.Add("Price check history not available (service not updated yet). If already updated, check SELECT permission on dbo.AzurePriceChecks.");
        var changes = Checks.Where(x => S(x,"outcome") == "Updated").GroupBy(x => S(x,"meterCode")).Select(g => g.Last()).ToArray();
        if (changes.Length == 0) lines.Add("Last change: none");
        foreach (var r in changes) lines.Add($"Last change: {S(r,"meterCode")} {Price(r,"currentPrice")} -> {Price(r,"observedPrice")} effective {S(r,"proposedEffectiveFrom")} (found {Date(At(r))})");
        lines.AddRange(Warnings().Select(x => "!! " + x));
        if (Warnings().Length > 0) lines.Add("Check https://prices.azure.com / Azure pricing page; review job deployment and permissions. Manual prices must use a future EffectiveFrom; existing rows are never overwritten.");
        return string.Join(Environment.NewLine, lines);
    }
    public static string Sql => SqlResources.Read("AzurePriceStatus.sql");
    public static async Task<AzurePriceStatus> Read(string server, string database, bool trust)
    {
        await using var sql = new SqlRunner(server,database,trust);
        var sets = await sql.Execute(Sql);
        return new(JsonNode.Parse(string.Concat(sets.SelectMany(x => x).SelectMany(x => x.Values).Select(x => x?.ToString())))!);
    }
    public static async Task<int> Run(OperatorArguments a, IProvisionConsole console, bool? trust)
    {
        var server = a.Has("sql-server") ? a.Get("sql-server") : CliPrompts.Ask(console,"SQL Server");
        var database = a.Has("database") ? a.Get("database") : CliPrompts.Ask(console,"Database","ISTDocAi");
        var trusted = trust ?? (a.Has("trust-server-certificate") || CliPrompts.Ask(console,"TrustServerCertificate? yes/no","no").Equals("yes",StringComparison.OrdinalIgnoreCase));
        var status = await Read(server,database,trusted);
        console.WriteLine(a.Has("json") ? status.ToJson().ToJsonString() : status.Render());
        return 0;
    }
    public JsonNode ToJson()
    {
        var result = Data.DeepClone();
        result["lastCompletedCheckAtUtc"] = LastCompletedCheckAtUtc?.ToString("O");
        result["neverRan"] = NeverRan; result["stale"] = Stale;
        var meters = new JsonArray();
        foreach (var meter in Meters)
        {
            var rows = Checks.Where(x => S(x,"meterCode") == meter).ToArray();
            var run = rows.Reverse().TakeWhile(x => S(x,"outcome") is "Refused" or "Failed").ToArray();
            meters.Add(new JsonObject { ["meterCode"] = meter, ["latestCheck"] = rows.LastOrDefault()?.DeepClone(),
                ["lastUpdated"] = rows.LastOrDefault(x => S(x,"outcome") == "Updated")?.DeepClone(),
                ["openWarning"] = run.Length == 0 ? null : new JsonObject { ["since"] = run.Min(At).ToString("O"), ["reason"] = Reason(S(run[0],"reasonCode")) } });
        }
        result["meters"] = meters;
        result["warnings"] = new JsonArray(Warnings().Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        return result;
    }
}
