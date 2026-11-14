using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class ManifestTotalsTests
{
    [TestCase("82902", 82902)]
    [TestCase("82,902", 82902)]
    [TestCase("82.902,00", 82902)]
    [TestCase("23.100,00", 23100)]
    [TestCase("82902.0", 82902)]
    [TestCase("4,005 CTN", 4005)]
    [TestCase("CTN 1335.0", 1335)]
    [TestCase("1 335", 1335)]
    [TestCase("1,234,567", 1234567)]
    [TestCase("1.234.567", 1234567)]
    [TestCase("12.5 MT", 12.5)]
    [TestCase("3 x", 3)]
    public void Parses_printed_numbers(string text, double expected)
    {
        Assert.That(DocAiServices.ManifestNumberParserService.TryParse(text, out var value), Is.True);
        Assert.That(value, Is.EqualTo((decimal)expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("garbage")]
    [TestCase("1x2")]
    [TestCase("1,23,456")]
    [TestCase("1..2")]
    [TestCase("-1")]
    [TestCase("1/2")]
    [TestCase("12 34")]
    [TestCase("1.2.3")]
    [TestCase("999999999999999999999999999999999999999999")]
    public void Rejects_ambiguous_or_garbage(string? text) => Assert.That(DocAiServices.ManifestNumberParserService.TryParse(text, out _), Is.False);
    private static ImmutableDictionary<string, ExtractedField> Fields(params (string Key, string Value)[] values) => values.ToImmutableDictionary(v => v.Key, v => new ExtractedField(v.Value, null, 1, 1));
    private static CanonicalManifestResult Map(string? count = "3", string? packages = "4005 CTN", string? weight = "82902 KGS", int linked = 3, string gross = "27634.0", string containerPackages = "1335")
    {
        var fields = Fields(("billOfLadingNumber", "ALSEEF"));
        foreach (var(key, value)in new[]
        {
            ("containerCount", count),
            ("packages", packages),
            ("grossWeight", weight)
        }

        )
            if (value is not null)
                fields = fields.Add(key, new(value, null, 1, 1));
        var containers = Enumerable.Range(0, linked).Select(i => new ManifestRow(i, Fields(("billOfLadingNumber", "ALSEEF"), ("packages", containerPackages), ("grossWeight", gross)), ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty)).ToImmutableArray();
        return DocAiServices.ManifestResultMapperService.Map(new(null, "manifest-v2", 1, Fields(), [new(0, fields, ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty)], containers), .8).Value!;
    }

    [Test]
    public void Alseef_correct_totals_pass() => Assert.That(Map().Flags, Is.Empty);
    [Test]
    public void Net_column_in_gross_is_exposed()
    {
        var flags = Map(gross: "26700.0").Flags;
        Assert.That(flags.Length, Is.EqualTo(2));
        Assert.That(flags, Does.Contain(new FieldFlag("billsOfLading[0].fields.grossWeight", FlagReasons.TotalsMismatch)));
        Assert.That(flags, Does.Contain(new FieldFlag("billsOfLading[0]", FlagReasons.TotalsMismatch)));
    }

    [Test]
    public void Wrong_count_is_exposed() => Assert.That(Map(linked: 2).Flags.Any(f => f.Field.EndsWith(".containerCount") && f.Reason == FlagReasons.TotalsMismatch), Is.True);
    [Test]
    public void Missing_totals_have_no_flags() => Assert.That(Map(null, null, null).Flags, Is.Empty);
    [Test]
    public void Invalid_total_is_informational() => Assert.That(PortAssertions.Single(Map(count: "unknown").Flags).Reason, Is.EqualTo(FlagReasons.TotalsUnverified));
    [Test]
    public void Zero_containers_without_count_cannot_verify_totals() => Assert.That(PortAssertions.Single(Map(count: null, linked: 0).Flags).Reason, Is.EqualTo(FlagReasons.TotalsUnverified));
    [Test]
    public void Missing_participant_cannot_verify_total() => Assert.That(PortAssertions.Single(Map(containerPackages: "unknown").Flags).Reason, Is.EqualTo(FlagReasons.TotalsUnverified));
    [TestCase("27772", false)]
    [TestCase("27773", true)]
    public void Weight_tolerance_is_half_percent(string gross, bool mismatch) => Assert.That(Map(gross: gross).Flags.Any(f => f.Reason == FlagReasons.TotalsMismatch), Is.EqualTo(mismatch));
    [Test]
    public void Fractional_count_cannot_be_verified() => Assert.That(Map(count: "3.5").Flags.Any(f => f.Reason == FlagReasons.TotalsUnverified), Is.True);
    [Test]
    public void Packages_are_exact() => Assert.That(Map(containerPackages: "1335.1").Flags.Any(f => f.Field.EndsWith(".packages") && f.Reason == FlagReasons.TotalsMismatch), Is.True);
    [TestCase("10", "11", false)]
    [TestCase("10", "11.01", true)]
    public void Weight_tolerance_has_one_kg_floor(string expected, string actual, bool mismatch)
    {
        var flags = DocAiServices.ManifestTotalsPolicyService.Check(Fields(("grossWeight", expected)), [new(Fields(("grossWeight", actual)), "blNumber")], "billsOfLading[0]");
        Assert.That(flags.Any(f => f.Reason == FlagReasons.TotalsMismatch), Is.EqualTo(mismatch));
    }

    [Test]
    public void Stated_zero_count_with_no_containers_passes() => Assert.That(Map("0", null, null, 0).Flags, Is.Empty);
    [Test]
    public void Unstated_totals_with_no_containers_have_no_flags() => Assert.That(Map(null, null, null, 0).Flags, Is.Empty);
    [Test]
    public void Sum_overflow_is_unverified() => Assert.That(Map(gross: decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)).Flags.Any(f => f.Reason == FlagReasons.TotalsUnverified), Is.True);
}
