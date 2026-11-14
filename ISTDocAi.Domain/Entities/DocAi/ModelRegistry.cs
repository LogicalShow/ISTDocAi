namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class ModelRegistry
{
    public string ModelId { get; set; } = null!;
    public string DocumentTypeCode { get; set; } = null!;
    public string? ShippingLineCode { get; set; }
    public string Kind { get; set; } = null!;
    public short Version { get; set; }
    public string Status { get; set; } = null!;
    public decimal MinFieldConfidence { get; set; }
    public decimal? TestFieldAccuracy { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
