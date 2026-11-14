namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed class AnalyzeResultMappingProfile : AnalyzeResultMappingOptions
{
    public string DocumentTypeCode { get; set; } = "";
    public Dictionary<string, string> HeaderFieldMap { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> BillFieldMap { get; set; } = new(StringComparer.Ordinal);
    public string BillsOfLadingField { get; set; } = "BillsOfLading";
}
