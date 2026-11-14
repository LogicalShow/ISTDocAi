namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record ClassMapping(string? ShippingLineCode, decimal MinClassConfidence)
{
    public string? TargetDocumentTypeCode { get; init; }
    private ClassMapping() : this(null, default) { }
}
