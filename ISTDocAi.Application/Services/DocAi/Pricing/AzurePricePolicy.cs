using System.Globalization;
namespace ISTDocAi.Application.Services.DocAi.Pricing;

public sealed record AzurePriceObservation(string? MeterId, string? MeterName, string? Currency, string? Region,
    string? Unit, string? Type, decimal? TierMinimumUnits, decimal? Price, DateTimeOffset? AzureEffectiveFrom,
    string? ProductName, string? SkuName, string? ServiceName);
public sealed record AzurePriceDecision(string MeterCode, AzurePriceObservation? Observation, int MatchedRowCount,
    string? ReasonCode, string? Source, IReadOnlyList<AzurePriceObservation>? Matches = null);
public sealed record AzurePriceCheck(Guid CheckId, DateTimeOffset StartedAtUtc, DateTimeOffset CheckedAtUtc,
    string Outcome, string? ReasonCode, IReadOnlyList<AzurePriceDecision> Decisions);
public sealed record AzurePriceCheckResult(string MeterCode, string Outcome, string? ReasonCode);

public static class AzurePricePolicy
{
    public const string ExtractId = "a69cec6a-f33e-50de-8352-02ae0cd5b902";
    public const string ClassifyId = "7d6b9877-3042-523e-b97d-7a4bce5b863f";
    public static readonly IReadOnlyList<string> MeterCodes = Array.AsReadOnly(new[] { "DI_CUSTOM_EXTRACT", "DI_CUSTOM_CLASSIFY" });
    public static IReadOnlyList<AzurePriceDecision> Evaluate(IReadOnlyList<AzurePriceObservation> rows, DateTimeOffset now)
    {
        return MeterCodes.Select((code, index) =>
        {
            var id = index == 0 ? ExtractId : ClassifyId;
            var name = index == 0 ? "S0 Custom Pages" : "S0 pages for doc classifier";
            // Include either identity: a wrong ID with the exact name is invalid, never silently ignored.
            var matches = rows.Where(r => r.MeterId == id || r.MeterName == name).ToArray();
            if (matches.Length != 1) return new AzurePriceDecision(code, null, matches.Length,
                matches.Length == 0 ? "METER_MISSING" : "METER_AMBIGUOUS", null, matches);
            var row = matches[0];
            string? reason = row.MeterId != id || row.MeterName != name || row.Currency != "USD" ||
                row.Region != "francecentral" || row.Unit != "1K" || row.Type != "Consumption" ||
                row.TierMinimumUnits != 0 || row.ProductName != "Azure Document Intelligence" ||
                row.SkuName != "S0" || row.ServiceName != "Foundry Tools" || row.AzureEffectiveFrom is null
                ? "INVALID_METER" : row.Price is null or <= 0 || row.Price > 99999.9999m ||
                decimal.Round(row.Price.Value, 4) != row.Price ? "INVALID_PRICE" :
                row.AzureEffectiveFrom > now ? "AZURE_PRICE_NOT_YET_EFFECTIVE" : null;
            var source = reason is null ? string.Create(CultureInfo.InvariantCulture,
                $"Azure Retail Prices API; France Central; meterId={id}; Azure effective={row.AzureEffectiveFrom:yyyy-MM-dd}; read={now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}") : null;
            if (source?.Length > 200) throw new InvalidOperationException("Price source exceeds storage limit.");
            return new AzurePriceDecision(code, row, 1, reason, source);
        }).ToArray();
    }
}
