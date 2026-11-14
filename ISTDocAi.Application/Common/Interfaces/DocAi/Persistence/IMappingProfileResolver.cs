namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IMappingProfileResolver
{
    // Only Map and fingerprinting historically allowed unknown explicit document types to use
    // the global map. Parse and typed selection retain MappingProfiles.Select's strict behavior.
    AnalyzeResultMappingOptions Resolve(string modelId, string documentType, bool allowLegacyDocumentType = false);
}

/// <summary>The existing appsettings selection rules, including legacy explicit document types.</summary>
