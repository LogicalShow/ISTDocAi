namespace ISTDocAi.Domain.Entities.DocAi;
public sealed class AzurePriceCheck
{
    public Guid CheckId { get; set; }
    public string MeterCode { get; set; } = null!;
    public DateTime StartedAtUtc { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public string Outcome { get; set; } = null!;
    public string? ReasonCode { get; set; }
    public decimal? ObservedPrice { get; set; }
    public decimal? CurrentPrice { get; set; }
    public DateTime? ProposedEffectiveFrom { get; set; }
    public int? MatchedRowCount { get; set; }
    public string? MeterId { get; set; }
    public string? Currency { get; set; }
    public string? Region { get; set; }
    public string? Unit { get; set; }
    public string? Type { get; set; }
    public decimal? TierMinimumUnits { get; set; }
    public DateTime? AzureEffectiveFrom { get; set; }
    public byte[]? WarningFingerprint { get; set; }
}
