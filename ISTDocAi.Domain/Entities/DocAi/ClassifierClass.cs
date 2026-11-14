namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class ClassifierClass
{
    public string ClassifierModelId { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string? ShippingLineCode { get; set; }
    public string? TargetDocumentTypeCode { get; set; }
}
