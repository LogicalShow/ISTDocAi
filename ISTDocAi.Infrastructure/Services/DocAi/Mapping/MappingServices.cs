using ISTDocAi.Application.Services.DocAi.Iso6346Service;
using ISTDocAi.Application.Services.DocAi.CanonicalResultMapperService;
using ISTDocAi.Application.Services.DocAi.ManifestResultMapperService;
using ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestTotalsPolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestNumberParserService;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;
internal static class MappingServices
{
    internal static readonly Iso6346Service Iso = new();
    private static readonly ManifestAssociationPolicyService Association = new();
    internal static readonly CanonicalResultMapperService Bill = new(Iso);
    internal static readonly ManifestResultMapperService Manifest = new(Iso, Association, new ManifestRowMergePolicyService(Association), new ManifestTotalsPolicyService(new ManifestNumberParserService()));
}
