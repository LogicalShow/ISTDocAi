namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class JobRun
{
    public string? MappingFingerprint { get; set; }
    public string? ChangesJson { get; set; }
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public string Reason { get; set; } = null!;
    public short ConsumerId { get; set; }
    public short PageCount { get; set; }
    public string? ClientRequestId { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ResolvedDocumentTypeCode { get; set; }
    public string? ResultType { get; set; }
}
