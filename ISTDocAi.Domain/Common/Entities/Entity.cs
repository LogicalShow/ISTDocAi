namespace ISTDocAi.Domain.Common.Entities;

/// <summary>
/// Base type for persisted domain entities that only require an identifier.
/// </summary>
public abstract class Entity
{
    public string Id { get; set; } = string.Empty;
}
