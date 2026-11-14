namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public class AnalyzeResultMappingOptions
{
    public Dictionary<string, string> FieldMap { get; set; } = new[]
    {
        "billOfLadingNumber", "shipperName", "consigneeName", "vesselName", "voyageNumber",
        "portOfLoading", "portOfDischarge", "issueDate"
    }.ToDictionary(n => n, n => n, StringComparer.Ordinal);
    public Dictionary<string, AnalyzeResultMappingProfile> Models { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ContainerFieldMap { get; set; } = new(StringComparer.Ordinal);
    public string ContainersField { get; set; } = "containers";
    public string ContainerNumberField { get; set; } = "containerNumber";
    public string SealNumberField { get; set; } = "sealNumber";
}
