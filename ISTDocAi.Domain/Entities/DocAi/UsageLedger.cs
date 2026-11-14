namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class UsageLedger
{
    public long UsageId { get; set; }
    public short ConsumerId { get; set; }
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public long OperationId { get; set; }
    public string MeterCode { get; set; } = null!;
    public string ModelId { get; set; } = null!;
    public int Pages { get; set; }
    public decimal? PricePer1000 { get; set; }
    public decimal? AzureCostUsd { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? BillingPeriod { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public long? ReversesUsageId { get; set; }
    public string CorrelationId { get; set; } = null!;
}
