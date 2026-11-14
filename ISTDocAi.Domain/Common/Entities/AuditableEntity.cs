namespace ISTDocAi.Domain.Common.Entities;

/// <summary>
/// Base type for mutable entities that require audit metadata and optimistic concurrency.
/// </summary>
public abstract class AuditableEntity : ConcurrentEntity
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
