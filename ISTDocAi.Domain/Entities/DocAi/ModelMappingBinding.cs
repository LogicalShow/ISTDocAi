namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class ModelMappingBinding
{
    public string ModelId { get; set; } = null!;
    public long ProfileId { get; set; }
}
