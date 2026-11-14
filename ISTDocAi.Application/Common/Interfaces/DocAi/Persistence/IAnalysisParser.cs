using ISTDocAi.Domain.Dtos.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
public interface IAnalysisParser
{
    MappedResult Map(string json, string documentType, string? line, string modelId, double minFieldConfidence);
    ExtractedDocument Parse(string json, string documentType, string? line, string modelId);
    ParsedClass ParseClassifier(string json);
}
