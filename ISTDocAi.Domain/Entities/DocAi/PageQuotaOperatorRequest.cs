namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class PageQuotaOperatorRequest
{
    public Guid RequestId { get; set; }
    public short ConsumerId { get; set; }
    public string Payload { get; set; } = null!;
    public DateTime RecordedAtUtc { get; set; }
}
