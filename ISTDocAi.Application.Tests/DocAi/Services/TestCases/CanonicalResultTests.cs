using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class CanonicalResultTests
{
    [TestCase("UACU 5636100", "UACU5636100")]
    [TestCase("UACU  5636100", "UACU5636100")]
    [TestCase("TEMU 895330-2", "TEMU8953302")]
    [TestCase("fanu 1134551", "FANU1134551")]
    [TestCase("FANU\u00a03266027", "FANU3266027")]
    [TestCase("TEMU8953302", "TEMU8953302")]
    [TestCase(" UACU.563/6100 ", "UACU5636100")]
    [TestCase(" bad / OCR ", "bad / OCR")]
    [TestCase("CSQA3054383", "CSQA3054383")]
    [TestCase(null, null)]
    public void Normalize_preserves_uncertain_input(string? input, string? expected) => Assert.That(DocAiServices.Iso6346Service.Normalize(input), Is.EqualTo(expected));
    [TestCase("UACU 5636100", "UACU5636100", false)]
    [TestCase("FANU 1134551", "FANU1134551", false)]
    [TestCase("TEMU 895330-2", "TEMU8953302", false)]
    [TestCase("FANU 3266027", "FANU3266027", false)]
    [TestCase("UACU 5636101", "UACU5636101", true)]
    public void Map_normalizes_before_check_digit_and_preserves_metadata(string input, string expected, bool invalid)
    {
        var result = DocAiServices.CanonicalResultMapperService.Map(Document(null, new ContainerFields(Field(input, .95, 2, input))), .8).Value!;
        Assert.That(PortAssertions.Single(result.Containers).ContainerNumber, Is.EqualTo(new ExtractedField(expected, input, .95, 2)));
        Assert.That(result.Flags.Any(f => f.Reason == FlagReasons.CheckDigit), Is.EqualTo(invalid));
    }

    private static ExtractedField Field(string? value = "text", double confidence = .9, int page = 1, string? raw = null) => new(value, raw, confidence, page);
    private static ExtractedDocument Document(ImmutableDictionary<string, ExtractedField?>? fields = null, params ContainerFields[] containers) => new("BL", null, "generic-model", 2, fields ?? ImmutableDictionary<string, ExtractedField?>.Empty, [..containers]);
    [TestCase(.79, true)]
    [TestCase(.8, false)]
    [TestCase(.81, false)]
    public void Threshold_is_strictly_below(double confidence, bool flagged)
    {
        var f = Field(confidence: confidence, raw: "Original text.");
        var r = DocAiServices.CanonicalResultMapperService.Map(Document(ImmutableDictionary<string, ExtractedField?>.Empty.Add("shipperName", f)), .8).Value!;
        Assert.That(r.FlaggedFields, Is.EqualTo(flagged ? 1 : 0));
        Assert.That(r.Fields["shipperName"], Is.EqualTo(f));
        Assert.That(r.Fields["shipperName"].RawText, Is.EqualTo("Original text."));
        Assert.That(r.ContractVersion, Is.EqualTo("1.0"));
        Assert.That(r.DocumentTypeCode, Is.EqualTo("BL"));
        Assert.That(r.ShippingLineCode, Is.Null);
        Assert.That(r.ModelId, Is.EqualTo("generic-model"));
        Assert.That(r.PageCount, Is.EqualTo(2));
        if (flagged)
            Assert.That(PortAssertions.Single(r.Flags), Is.EqualTo(new FieldFlag("shipperName", FlagReasons.LowConfidence)));
    }

    [Test]
    public void Missing_fields_are_omitted_and_present_null_values_are_preserved()
    {
        var d = Document(ImmutableDictionary<string, ExtractedField?>.Empty.Add("missing", null).Add("present", Field(null)));
        var r = DocAiServices.CanonicalResultMapperService.Map(d, .8).Value!;
        Assert.That(r.Fields.ContainsKey("missing"), Is.False);
        Assert.That(r.Fields["present"].Value, Is.Null);
        Assert.That(r.Flags, Is.Empty);
    }

    [Test]
    public void Empty_result_is_valid_and_does_not_invent_fields_or_containers()
    {
        var r = DocAiServices.CanonicalResultMapperService.Map(Document(), .8).Value!;
        Assert.That(r.Fields, Is.Empty);
        Assert.That(r.Containers, Is.Empty);
        Assert.That(r.Flags, Is.Empty);
        Assert.That(r.FlaggedFields, Is.EqualTo(0));
    }

    [Test]
    public void All_low_fields_and_container_fields_are_flagged_with_distinct_count()
    {
        var input = Document(ImmutableDictionary<string, ExtractedField?>.Empty.Add("shipperName", Field(confidence: .2)).Add("issueDate", Field(confidence: .1, page: 2)), new ContainerFields(Field("CSQU3054384", .2), Field("SEAL", .4)), new ContainerFields(Field("CSQU3054383", .8)), new ContainerFields(Field("MSKU1234565", .7), Field("OTHER", .8)));
        var r = DocAiServices.CanonicalResultMapperService.Map(input, .8).Value!;
        Assert.That(r.Flags.Length, Is.EqualTo(6));
        Assert.That(r.FlaggedFields, Is.EqualTo(5));
        Assert.That(r.Containers.Length, Is.EqualTo(3));
        Assert.That(r.Flags, Does.Contain(new FieldFlag("containers[0].containerNumber", FlagReasons.LowConfidence)));
        Assert.That(r.Flags, Does.Contain(new FieldFlag("containers[0].containerNumber", FlagReasons.CheckDigit)));
        Assert.That(r.Flags, Does.Contain(new FieldFlag("containers[0].sealNumber", FlagReasons.LowConfidence)));
        Assert.That(r.Flags, Does.Contain(new FieldFlag("containers[2].containerNumber", FlagReasons.LowConfidence)));
        Assert.That(r.Flags.Any(f => f.Field == "containers[1].containerNumber"), Is.False);
        Assert.That(r.Flags.Any(f => f.Field == "containers[2].sealNumber"), Is.False);
        Assert.That(r.Containers[1].SealNumber, Is.Null);
    }

    [TestCase(-.1)]
    [TestCase(1.1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void Invalid_threshold_and_field_confidence_return_rejection(double confidence)
    {
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(), confidence).Rejection, Is.EqualTo(Rejection.InvalidInput));
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(ImmutableDictionary<string, ExtractedField?>.Empty.Add("bad", Field(confidence: confidence))), .8).IsSuccess, Is.False);
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(null, new ContainerFields(Field(confidence: confidence))), .8).IsSuccess, Is.False);
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(null, new ContainerFields(Field("CSQU3054383"), Field(confidence: confidence))), .8).IsSuccess, Is.False);
    }

    [TestCase(0)]
    [TestCase(3)]
    public void Fields_must_reference_an_actual_page(int page)
    {
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(ImmutableDictionary<string, ExtractedField?>.Empty.Add("bad", Field(page: page))), .8).IsSuccess, Is.False);
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(null, new ContainerFields(Field("CSQU3054383", page: page))), .8).IsSuccess, Is.False);
    }

    [Test]
    public void Threshold_extremes_and_missing_container_number_value()
    {
        var d = Document(null, new ContainerFields(Field(null, 0)));
        var r = DocAiServices.CanonicalResultMapperService.Map(d, 0).Value!;
        Assert.That(PortAssertions.Single(r.Flags), Is.EqualTo(new FieldFlag("containers[0].containerNumber", FlagReasons.CheckDigit)));
        Assert.That(DocAiServices.CanonicalResultMapperService.Map(Document(null, new ContainerFields(Field("CSQU3054383", 1))), 1).IsSuccess, Is.True);
    }

    [TestCase("CSQU3054383", true)]
    [TestCase("MSKU1234565", true)]
    [TestCase("CSQU3054384", false)]
    [TestCase("MSKU1234560", false)]
    [TestCase("CSQA3054383", false)]
    [TestCase("csqu3054383", false)]
    [TestCase("CSQU305438", false)]
    [TestCase("CSQU30543833", false)]
    [TestCase("CS1U3054383", false)]
    [TestCase("CSQU30543A3", false)]
    [TestCase("CSQU\uFF13\uFF10\uFF15\uFF14\uFF13\uFF183", false)]
    [TestCase(" CSQU3054383", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void ISO_known_numbers_and_invalid_formats(string? number, bool expected) => Assert.That(DocAiServices.Iso6346Service.IsValid(number), Is.EqualTo(expected));
    [Test]
    public void ISO_all_letter_values_categories_and_remainder_ten()
    {
        // Independent ISO letter table (11, 22 and 33 are omitted).
        int[] values = [10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 34, 35, 36, 37, 38];
        foreach (var owner in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            foreach (var category in "UJZ")
            {
                var prefix = $"{owner}AA{category}000000";
                var sum = values[owner - 'A'] + 10 * 2 + 10 * 4 + values[category - 'A'] * 8;
                var digit = sum % 11 % 10;
                Assert.That(DocAiServices.Iso6346Service.IsValid(prefix + digit), Is.True);
                Assert.That(DocAiServices.Iso6346Service.IsValid(prefix + ((digit + 1) % 10)), Is.False);
            }

        // A=10, A=10, A=10, U=32 gives 326 % 11 = 7; serial 000006 adds 3072 -> remainder 10 -> check 0.
        Assert.That(DocAiServices.Iso6346Service.IsValid("AAAU0000060"), Is.True);
    }

    [Test]
    public void Canonical_collections_do_not_alias_mutable_input_builders()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ExtractedField?>();
        builder.Add("field", Field());
        var d = Document(builder.ToImmutable());
        var result = DocAiServices.CanonicalResultMapperService.Map(d, .8).Value!;
        builder.Clear();
        PortAssertions.Single(result.Fields);
        PortAssertions.Single(d.Fields);
    }
}
