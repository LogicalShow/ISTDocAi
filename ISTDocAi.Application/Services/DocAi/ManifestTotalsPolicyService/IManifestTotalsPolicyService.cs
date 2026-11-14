using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestTotalsPolicyService;
public interface IManifestTotalsPolicyService
{
    ImmutableArray<FieldFlag> Check(ImmutableDictionary<string, ExtractedField> fields, ImmutableArray<ManifestContainer> containers, string path);
}
