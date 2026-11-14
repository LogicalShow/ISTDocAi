using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;
public sealed class ManifestAssociationPolicyService : IManifestAssociationPolicyService
{
    public string Identifier(ManifestRow row) => row.Fields.GetValueOrDefault("billOfLadingNumber")?.Value?.Trim() ?? "";
    public ManifestPosition? Anchor(ManifestRow row, int pages) => row.Geometry.Values.SelectMany(r => r).Where(r => r.Page >= 1 && r.Page <= pages && !r.Polygon.IsDefault && r.Polygon.Length >= 6 && r.Polygon.Length % 2 == 0 && r.Polygon.All(v => double.IsFinite(v) && v >= 0)).Select(r => new ManifestPosition(r.Page, r.Polygon.Where((_, i) => i % 2 == 1).Min())).Order().FirstOrDefault();
    public ImmutableArray<ManifestRow> Sort(IEnumerable<ManifestRow> rows, int pages) => rows.OrderBy(r => Anchor(r, pages)is null).ThenBy(r => Anchor(r, pages)).ThenBy(r => r.SourceIndex).ToImmutableArray();
    public ManifestAssociation Associate(ManifestRow container, ImmutableArray<ManifestRow> bills, int pages)
    {
        var reference = Identifier(container);
        if (reference.Length > 0)
        {
            var matches = Enumerable.Range(0, bills.Length).Where(i => string.Equals(Identifier(bills[i]), reference, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length switch
            {
                1 => new(matches[0], "blNumber", []),
                0 => new(null, null, [FlagReasons.OrphanContainer, FlagReasons.BlReferenceNotFound]),
                _ => new(null, null, [FlagReasons.AmbiguousBlReference])};
        }

        var position = Anchor(container, pages);
        if (position is null)
            return new(null, null, [FlagReasons.AssociationPositionMissing]);
        var eligible = Enumerable.Range(0, bills.Length).Select(i => (Index: i, Anchor: Anchor(bills[i], pages))).Where(b => b.Anchor is not null && b.Anchor.CompareTo(position) <= 0).ToArray();
        if (eligible.Length == 0)
            return new(null, null, [FlagReasons.OrphanContainer]);
        var latest = eligible.Max(b => b.Anchor)!;
        var owners = eligible.Where(b => b.Anchor == latest).ToArray();
        return owners.Length == 1 ? new(owners[0].Index, "position", [FlagReasons.AssociationByPosition]) : new(null, null, [FlagReasons.AmbiguousBlReference]);
    }
}
