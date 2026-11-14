using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;
public interface IManifestAssociationPolicyService
{
    string Identifier(ManifestRow row);
    ManifestPosition? Anchor(ManifestRow row, int pages);
    ImmutableArray<ManifestRow> Sort(IEnumerable<ManifestRow> rows, int pages);
    ManifestAssociation Associate(ManifestRow container, ImmutableArray<ManifestRow> bills, int pages);
}
