namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class PageReservation
{
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public short ConsumerId { get; set; }
    public long Pages { get; set; }
    public string State { get; set; } = null!;
    public DateTime ReservedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public long? OperationId { get; set; }
}
