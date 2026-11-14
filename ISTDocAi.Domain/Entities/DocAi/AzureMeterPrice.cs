namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class AzureMeterPrice
{
    public string MeterCode { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public decimal PricePer1000 { get; set; }
    public string Currency { get; set; } = null!;
    public string Source { get; set; } = null!;
}
