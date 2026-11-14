namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class PageAllowanceGrant
{
    public long GrantId { get; set; }
    public short ConsumerId { get; set; }
    public long Pages { get; set; }
    public string Kind { get; set; } = null!;
    public long? OriginalGrantId { get; set; }
    public string OperatorIdentity { get; set; } = null!;
    public string Note { get; set; } = null!;
    public Guid RequestId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public long BalanceAfter { get; set; }
}
