namespace ISTDocAi.Domain.Common.Entities;

/// <summary>
/// Base type for entities that require optimistic concurrency control.
/// </summary>
public abstract class ConcurrentEntity : Entity
{
    public byte[] RowVersion { get; set; } = [];
}
