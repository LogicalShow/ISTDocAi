namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class Job
{
    public long JobSeq { get; set; }
    public Guid JobId { get; set; }
    public short ConsumerId { get; set; }
    public short RunNo { get; set; }
    public string? ClientRequestId { get; set; }
    public string DocumentTypeCode { get; set; } = null!;
    public string? RequestedShippingLineCode { get; set; }
    public string IdemLine { get; set; } = null!;
    public string? ShippingLineCode { get; set; }
    public byte[] FileSha256 { get; set; } = null!;
    public string? FileName { get; set; }
    public string ContentType { get; set; } = null!;
    public int FileSizeBytes { get; set; }
    public short PageCount { get; set; }
    public string BlobPath { get; set; } = null!;
    public string? ModelId { get; set; }
    public string Stage { get; set; } = null!;
    public string Status { get; set; } = null!;
    public byte Attempts { get; set; }
    public byte TakeoverCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public string? ResolvedDocumentTypeCode { get; set; }
}
