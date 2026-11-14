using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService;
public interface IManifestRowMergePolicyService
{
    ManifestRowMergeResult Merge(ImmutableArray<ManifestRow> rows, int pages);
}
