namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class MonthlyConsumerCost
{
    public string BillingPeriod { get; set; } = null!;
    public short ConsumerId { get; set; }
    public int Jobs { get; set; }
    public int ExtractPages { get; set; }
    public int ClassifyPages { get; set; }
    public decimal PageShare { get; set; }
    public decimal DiExtractCostUsd { get; set; }
    public decimal DiClassifyCostUsd { get; set; }
    public decimal SharedCostUsd { get; set; }
    public decimal? TotalCostUsd { get; set; }
}
