using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;

namespace ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService;
public sealed class ManifestRowMergePolicyService(IManifestAssociationPolicyService _manifestAssociationPolicyService) : IManifestRowMergePolicyService
{
    private sealed record Bounds(double Left, double Right, double Top, double Bottom);
    public ManifestRowMergeResult Merge(ImmutableArray<ManifestRow> rows, int pages)
    {
        var sorted = _manifestAssociationPolicyService.Sort(rows, pages);
        var result = ImmutableArray.CreateBuilder<ManifestRow>();
        var changes = ImmutableDictionary.CreateBuilder<int, ImmutableHashSet<string>>();
        for (var index = 0; index < sorted.Length; index++)
        {
            var row = sorted[index];
            var nextBill = sorted.Skip(index + 1).FirstOrDefault(r => _manifestAssociationPolicyService.Identifier(r).Length > 0);
            var nextAnchor = nextBill is null ? null : _manifestAssociationPolicyService.Anchor(nextBill, pages);
            var rowAnchor = _manifestAssociationPolicyService.Anchor(row, pages);
            // Only the immediately preceding row can own a continuation.
            if ((nextAnchor is not null && rowAnchor is not null && rowAnchor.CompareTo(nextAnchor) >= 0) || result.Count == 0 || _manifestAssociationPolicyService.Identifier(row).Length > 0 || _manifestAssociationPolicyService.Identifier(result[^1]).Length == 0 || !TryMerge(result[^1], row, pages, out var merged))
            {
                result.Add(row);
                continue;
            }

            result[^1] = merged;
            changes[merged.SourceIndex] = changes.GetValueOrDefault(merged.SourceIndex, ImmutableHashSet<string>.Empty).Union(row.Fields.Keys);
        }

        return new(result.ToImmutable(), changes.ToImmutable());
    }

    private bool TryMerge(ManifestRow bill, ManifestRow continuation, int pages, out ManifestRow merged)
    {
        merged = bill;
        var b = _manifestAssociationPolicyService.Anchor(bill, pages);
        var u = _manifestAssociationPolicyService.Anchor(continuation, pages);
        if (b is null || u is null || b.Page != u.Page || u.Top <= b.Top || continuation.Fields.Count == 0 || continuation.Fields.Values.Any(f => f.Page != b.Page) || continuation.Geometry.Values.SelectMany(r => r).Any(r => r.Page != b.Page))
            return false;
        foreach (var(name, field)in continuation.Fields)
            if (bill.Fields.TryGetValue(name, out var prior) && !string.IsNullOrWhiteSpace(prior.Value) && !Continuous(bill.Geometry.GetValueOrDefault(name, []), continuation.Geometry.GetValueOrDefault(name, []), b.Page))
                return false;
        var fields = bill.Fields.ToBuilder();
        var geometry = bill.Geometry.ToBuilder();
        foreach (var(name, field)in continuation.Fields)
        {
            fields[name] = fields.TryGetValue(name, out var prior) && !string.IsNullOrWhiteSpace(prior.Value) ? new(Join(prior.Value, field.Value), Join(prior.RawText, field.RawText), Math.Min(prior.Confidence, field.Confidence), b.Page) : field with
            {
                Page = b.Page
            };
        }

        foreach (var(name, regions)in continuation.Geometry)
            geometry[name] = geometry.GetValueOrDefault(name, []).AddRange(regions);
        merged = bill with
        {
            Fields = fields.ToImmutable(),
            Geometry = geometry.ToImmutable()
        };
        return true;
    }

    private string? Join(string? first, string? second) => string.IsNullOrWhiteSpace(first) ? second : string.IsNullOrWhiteSpace(second) ? first : first.Trim() + " " + second.Trim();
    private Bounds? Box(ImmutableArray<CellRegion> regions, int page)
    {
        if (regions.IsDefaultOrEmpty || regions.Any(r => r.Page != page || r.Polygon.IsDefaultOrEmpty || r.Polygon.Length < 6 || r.Polygon.Length % 2 != 0 || r.Polygon.Any(v => !double.IsFinite(v) || v < 0)))
            return null;
        var x = regions.SelectMany(r => r.Polygon.Where((_, i) => i % 2 == 0)).ToArray();
        var y = regions.SelectMany(r => r.Polygon.Where((_, i) => i % 2 == 1)).ToArray();
        return new(x.Min(), x.Max(), y.Min(), y.Max());
    }

    private bool Continuous(ImmutableArray<CellRegion> prior, ImmutableArray<CellRegion> next, int page)
    {
        var b = Box(prior, page);
        var u = Box(next, page);
        if (b is null || u is null || b.Right <= b.Left || u.Right <= u.Left || b.Bottom <= b.Top)
            return false;
        return Math.Min(b.Right, u.Right) - Math.Max(b.Left, u.Left) >= .5 * Math.Min(b.Right - b.Left, u.Right - u.Left) && u.Top > b.Bottom && u.Top - b.Bottom <= 1.5 * (b.Bottom - b.Top);
    }
}
