namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class MappingProfile
{
    public long ProfileId { get; set; }
    public string ProfileCode { get; set; } = null!;
    public int ProfileVersion { get; set; }
    public string DocumentTypeCode { get; set; } = null!;
    public string MappingShape { get; set; } = null!;
    public string ContainersField { get; set; } = null!;
    public string ContainerNumberField { get; set; } = null!;
    public string SealNumberField { get; set; } = null!;
    public string BillsOfLadingField { get; set; } = null!;
    public DateTime? SealedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string ChangeReference { get; set; } = null!;
}
