using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestResultMapperService;
public interface IManifestResultMapperService
{
    Decision<CanonicalManifestResult> Map(ExtractedManifest? document, double minFieldConfidence);
}
