using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;
public sealed class AnalysisParser(IMappingProfileResolver resolver) : IAnalysisParser
{
    public MappedResult Map(string json, string documentType, string? line, string modelId, double minFieldConfidence)
    {
        if (documentType == "AUTO") throw new InvalidOperationException("Extraction route is unresolved.");
        var profile = resolver.Resolve(modelId, documentType, allowLegacyDocumentType: true);
        return ModelResultMapper.MapEffective(json, documentType, line, modelId, minFieldConfidence, profile) with
        { MappingFingerprint = MappingFingerprint.ComputeEffective(profile, documentType) };
    }
    public ExtractedDocument Parse(string json, string documentType, string? line, string modelId) =>
        AnalyzeResultParser.Parse(json, documentType, line, modelId, resolver.Resolve(modelId, documentType));
    public ParsedClass ParseClassifier(string json)
    {
        var parsed = AnalyzeResultParser.ParseClassifier(json);
        return new(parsed.DocType, parsed.Confidence);
    }
}

