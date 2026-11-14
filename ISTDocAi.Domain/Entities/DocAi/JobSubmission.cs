namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class JobSubmission
{
    public short ConsumerId { get; set; }
    public string ClientRequestId { get; set; } = null!;
    public Guid JobId { get; set; }
    public short RunNo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
