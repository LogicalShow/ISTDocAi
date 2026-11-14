namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class AzureOperation
{
    public long OperationId { get; set; }
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public string Kind { get; set; } = null!;
    public string ModelId { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? OperationLocation { get; set; }
    public int? Pages { get; set; }
    public string? ClassName { get; set; }
    public decimal? ClassConfidence { get; set; }
    public string? RawResultPath { get; set; }
    public string? AzureErrorCode { get; set; }
    public DateTime SentAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ResultDeletedAtUtc { get; set; }
}
