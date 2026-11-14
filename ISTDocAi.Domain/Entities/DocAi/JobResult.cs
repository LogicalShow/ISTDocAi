namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class JobResult
{
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public string ContractVersion { get; set; } = null!;
    public string ResultJson { get; set; } = null!;
    public short FlaggedFields { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
