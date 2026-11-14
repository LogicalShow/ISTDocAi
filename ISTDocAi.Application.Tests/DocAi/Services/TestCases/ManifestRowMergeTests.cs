using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class ManifestRowMergeTests
{
    private static ManifestRow Row(int index, string? number, int page, double top, double left = 1, double confidence = .9)
    {
        var fields = ImmutableDictionary<string, ExtractedField>.Empty.Add("shipperName", new(index == 0 ? "FIRST" : "SECOND", index == 0 ? "FIRST" : "SECOND", confidence, page));
        if (number is not null)
            fields = fields.Add("billOfLadingNumber", new(number, number, .99, page));
        return new(index, fields, ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty.Add("shipperName", [new(page, [left, top, left + 1, top, left + 1, top + .1, left, top + .1])]));
    }

    [TestCase(1, 1.14, 1, null, true)]
    [TestCase(1, 1.14, 1.5, null, true)]
    [TestCase(1, 1.14, 1.51, null, false)]
    [TestCase(1, 1.26, 1, null, false)]
    [TestCase(1, 1.05, 1, null, false)]
    [TestCase(2, 1.14, 1, null, false)]
    [TestCase(2, 8, 1, null, false)]
    [TestCase(1, 1.14, 1, "OWN-BL", false)]
    public void Only_same_page_vertical_continuations_merge(int page, double top, double left, string? number, bool expected)
    {
        var input = new ExtractedManifest(null, "manifest-v2", 2, ImmutableDictionary<string, ExtractedField>.Empty, [Row(0, "BL", 1, 1), Row(1, number, page, top, left, .4)], []);
        var result = DocAiServices.ManifestResultMapperService.Map(input, .8).Value!;
        Assert.That(result.Flags.Any(f => f.Reason == FlagReasons.RowMerged), Is.EqualTo(expected));
        if (expected)
        {
            Assert.That(result.UnidentifiedBillsOfLading, Is.Empty);
            var field = result.BillsOfLading[0].Fields["shipperName"];
            Assert.That(field.Value, Is.EqualTo("FIRST SECOND"));
            Assert.That(field.RawText, Is.EqualTo("FIRST SECOND"));
            Assert.That(field.Confidence, Is.EqualTo(.4));
            Assert.That(result.Flags.Any(f => f.Field == "billsOfLading[0].fields.shipperName" && f.Reason == FlagReasons.LowConfidence), Is.True);
        }
        else if (number is null)
            PortAssertions.Single(result.UnidentifiedBillsOfLading);
        else
            Assert.That(result.BillsOfLading.Length, Is.EqualTo(2));
    }

    [Test]
    public void Candidate_at_next_identified_anchor_is_not_merged()
    {
        var result = DocAiServices.ManifestRowMergePolicyService.Merge([Row(0, "BL", 1, 1), Row(1, null, 1, 1.14), Row(2, "NEXT", 1, 1.14)], 2);
        Assert.That(result.Rows.Length, Is.EqualTo(3));
        Assert.That(result.ChangedFields, Is.Empty);
    }

    [Test]
    public void Missing_geometry_cannot_prove_conflicting_text_is_continuous()
    {
        var next = Row(1, null, 1, 1.14);
        next = next with
        {
            Geometry = next.Geometry.Remove("shipperName").Add("other", [new(1, [1, 1.14, 2, 1.14, 2, 1.24, 1, 1.24])])
        };
        Assert.That(DocAiServices.ManifestRowMergePolicyService.Merge([Row(0, "BL", 1, 1), next], 2).ChangedFields, Is.Empty);
    }

    [Test]
    public void One_conflicting_field_rejects_entire_row_including_missing_fields()
    {
        var next = Row(1, null, 1, 2)with
        {
            Fields = Row(1, null, 1, 2).Fields.Add("bookingNumber", new("BOOK", null, 1, 1))
        };
        var result = DocAiServices.ManifestRowMergePolicyService.Merge([Row(0, "BL", 1, 1), next], 2);
        Assert.That(result.Rows.Length, Is.EqualTo(2));
        Assert.That(result.Rows[0].Fields.ContainsKey("bookingNumber"), Is.False);
        Assert.That(result.ChangedFields, Is.Empty);
    }
}
