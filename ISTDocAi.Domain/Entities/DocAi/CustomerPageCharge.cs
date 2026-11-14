namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class CustomerPageCharge
{
    public long ChargeId { get; set; }
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public short ConsumerId { get; set; }
    public long Pages { get; set; }
    public long UsageId { get; set; }
    public long OperationId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
