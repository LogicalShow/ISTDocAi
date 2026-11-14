namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class MappingField
{
    public long ProfileId { get; set; }
    public string MapKind { get; set; } = null!;
    public string CanonicalKey { get; set; } = null!;
    public string AzureName { get; set; } = null!;
}
