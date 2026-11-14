namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class WebhookDelivery
{
    public long DeliveryId { get; set; }
    public Guid EventId { get; set; }
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public short ConsumerId { get; set; }
    public string EventType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public byte Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public short? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
}
