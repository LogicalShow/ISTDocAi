using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class ManifestAssociationTests
{
    private static ManifestRow Row(int index, string? identifier, int page = 1, double y = 1) => new(index, identifier is null ? ImmutableDictionary<string, ExtractedField>.Empty : ImmutableDictionary<string, ExtractedField>.Empty.Add("billOfLadingNumber", new(identifier, null, .8, 1)), ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty.Add("cell", [new(page, [0, y, 1, y, 1, y + 1, 0, y + 1])]));
    [Test]
    public void Anchor_uses_all_cells_and_earliest_valid_region_not_identifier_page_fallback()
    {
        var row = Row(0, "BL")with
        {
            Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty.Add("emptyIdentifier", [new(2, [0, 4, 1, 4, 1, 5])]).Add("description", [new(1, [0, 3, 1, 3, 1, 4]), new(1, [0, 2, 1, 2, 1, 3])])
        };
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Anchor(row, 2), Is.EqualTo(new ManifestPosition(1, 2)));
    }

    [TestCase(0, 1, 6)]
    [TestCase(3, 1, 6)]
    [TestCase(1, -1, 6)]
    [TestCase(1, 1, 5)]
    [TestCase(1, double.NaN, 6)]
    public void Invalid_geometry_never_creates_position(int page, double y, int length)
    {
        var row = Row(0, null)with
        {
            Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty.Add("cell", [new(page, Enumerable.Repeat(y, length).ToImmutableArray())])
        };
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Anchor(row, 2), Is.Null);
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Associate(row, [Row(0, "BL")], 2).Reasons, Does.Contain(FlagReasons.AssociationPositionMissing));
    }

    [Test]
    public void Exact_anchor_tie_stays_ambiguous_until_next_distinct_bill_anchor()
    {
        var bills = ImmutableArray.Create(Row(0, "A"), Row(1, "B"), Row(2, "C", y: 3));
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Associate(Row(0, null, y: 2), bills, 2).BillIndex, Is.Null);
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Associate(Row(0, null, y: 3), bills, 2).BillIndex, Is.EqualTo(2));
    }

    [Test]
    public void Missing_bill_position_does_not_prevent_explicit_match()
    {
        var bill = Row(0, "A")with
        {
            Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty
        };
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Associate(Row(0, "a"), [bill], 2).BillIndex, Is.EqualTo(0));
        Assert.That(DocAiServices.ManifestAssociationPolicyService.Associate(Row(0, null), [bill], 2).BillIndex, Is.Null);
    }

    [TestCase(-.1)]
    [TestCase(1.1)]
    [TestCase(double.NaN)]
    public void Invalid_threshold_is_rejected(double threshold)
    {
        var document = new ExtractedManifest(null, "model", 2, ImmutableDictionary<string, ExtractedField>.Empty, [], []);
        Assert.That(DocAiServices.ManifestResultMapperService.Map(document, threshold).IsSuccess, Is.False);
    }

    [Test]
    public void Threshold_boundary_and_found_null_values_are_preserved()
    {
        var row = Row(0, "BL");
        var container = Row(0, null, y: 2)with
        {
            Fields = ImmutableDictionary<string, ExtractedField>.Empty.Add("description", new(null, "original", .8, 1))
        };
        var document = new ExtractedManifest(null, "model", 2, ImmutableDictionary<string, ExtractedField>.Empty, [row], [container]);
        var result = DocAiServices.ManifestResultMapperService.Map(document, .8).Value!;
        Assert.That(result.Flags.Any(f => f.Reason == FlagReasons.LowConfidence), Is.False);
        Assert.That(result.BillsOfLading[0].Containers[0].Fields["description"].Value, Is.Null);
        Assert.That(result.BillsOfLading[0].Containers[0].Fields["description"].RawText, Is.EqualTo("original"));
    }

    [Test]
    public void Unidentified_rows_preserve_fields_sort_by_anchor_then_index_and_never_own_containers()
    {
        var fields = ImmutableDictionary<string, ExtractedField>.Empty.Add("shipperName", new("DELTA PRODUCE LOGISTICS S.A.E.", "Original shipper", .2, 1)).Add("description", new(null, "Original description", .8, 1));
        var early = Row(9, null, y: 1)with
        {
            Fields = fields
        };
        var tie = Row(3, "  ", y: 1);
        var later = Row(0, null, y: 3);
        var missing = Row(1, null)with
        {
            Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty
        };
        var identified = Row(10, "BL", y: 5);
        var document = new ExtractedManifest(null, "model", 2, ImmutableDictionary<string, ExtractedField>.Empty, [missing, later, early, identified, tie], [Row(0, null, y: 4)]);
        var result = DocAiServices.ManifestResultMapperService.Map(document, .8).Value!;
        Assert.That(result.UnidentifiedBillsOfLading.Select(b => b.SourceIndex), Is.EqualTo(new[] { 3, 9, 0, 1 }));
        Assert.That(result.UnidentifiedBillsOfLading[1].Fields, Is.EqualTo(fields));
        PortAssertions.Single(result.BillsOfLading);
        Assert.That(result.BillsOfLading[0].Containers, Is.Empty);
        PortAssertions.Single(result.UnassignedContainers);
        Assert.That(result.Flags.Count(f => f.Reason == FlagReasons.BlRowDropped), Is.EqualTo(4));
        Assert.That(result.Flags, Does.Contain(new FieldFlag("unidentifiedBillsOfLading[1].fields.shipperName", FlagReasons.LowConfidence)));
        Assert.That(result.Flags.Any(f => f.Field == "unidentifiedBillsOfLading[1].fields.description"), Is.False);
        Assert.That(result.Flags.Any(f => f.Field.StartsWith("source.", StringComparison.Ordinal)), Is.False);
        for (var i = 0; i < 4; i++)
            Assert.That(result.Flags, Does.Contain(new FieldFlag($"unidentifiedBillsOfLading[{i}]", FlagReasons.BlRowDropped)));
        var reversed = DocAiServices.ManifestResultMapperService.Map(document with { BillsOfLading = document.BillsOfLading.Reverse().ToImmutableArray() }, .8).Value!;
        Assert.That(reversed.UnidentifiedBillsOfLading.ToArray(), Is.EqualTo(result.UnidentifiedBillsOfLading.ToArray()));
        Assert.That(reversed.Flags.ToArray(), Is.EqualTo(result.Flags.ToArray()));
    }
}
