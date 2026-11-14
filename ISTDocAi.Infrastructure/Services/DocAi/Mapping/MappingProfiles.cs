using Microsoft.Extensions.Options;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public static class MappingProfiles
{
    public static AnalyzeResultMappingOptions Select(AnalyzeResultMappingOptions options, string modelId, string documentType)
    {
        if (options.Models.TryGetValue(modelId, out var profile))
        {
            if (!string.Equals(profile.DocumentTypeCode, documentType, StringComparison.Ordinal)) throw ConfigurationError();
            return profile;
        }
        if (documentType == "BL") return options;
        throw ConfigurationError();
    }

    public static AnalyzeResultMappingProfile SelectManifest(AnalyzeResultMappingOptions options, string modelId)
    {
        if (options.Models.TryGetValue(modelId, out var profile) && profile.DocumentTypeCode == "MANIFEST") return profile;
        throw ConfigurationError();
    }

    internal static OptionsValidationException ConfigurationError() => new(nameof(AnalyzeResultMappingOptions),
        typeof(AnalyzeResultMappingOptions), ["A matching mapping profile is required for the producing model and document type."]);
}
