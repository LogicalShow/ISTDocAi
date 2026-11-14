using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class InMemoryMappingProfileResolver(IOptions<AnalyzeResultMappingOptions> options) : IMappingProfileResolver
{
    public InMemoryMappingProfileResolver() : this(Options.Create(new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Mapping", "profile-inputs.json"))
        .Build().Get<AnalyzeResultMappingOptions>()!)) { }

    public static string Fingerprint(AnalyzeResultMappingOptions options, string modelId, string documentType,
        string algorithmVersion = MappingFingerprint.MappingAlgorithmVersion) =>
        MappingFingerprint.ComputeEffective(new InMemoryMappingProfileResolver(Options.Create(options))
            .Resolve(modelId, documentType, allowLegacyDocumentType: true), documentType, algorithmVersion);

    public AnalyzeResultMappingOptions Resolve(string modelId, string documentType, bool allowLegacyDocumentType = false)
    {
        var snapshot = options.Value;
        return !allowLegacyDocumentType || documentType is "BL" or "MANIFEST" || snapshot.Models.ContainsKey(modelId)
            ? MappingProfiles.Select(snapshot, modelId, documentType) : snapshot;
    }
}
