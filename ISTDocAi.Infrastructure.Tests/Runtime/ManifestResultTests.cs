using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class ManifestResultTests
{
    [Test]
    public void Spaced_container_associates_once_and_preserves_printed_text()
    {
        const string json = """
            {"pages":[{}],"documents":[{"fields":{
              "BillsOfLading":{"valueArray":[{"valueObject":{"BLNumber":{"valueString":"BILL1"}}}]},
              "Containers":{"valueArray":[{"valueObject":{
                "BLNumber":{"valueString":"BILL1"},
                "ContainerNo":{"valueString":"UACU  5636100","content":"UACU  5636100","confidence":0.99}
              }}]}
            }}]}
            """;
        var profile = new AnalyzeResultMappingProfile {
            DocumentTypeCode = "MANIFEST", ContainersField = "Containers",
            BillFieldMap = new() { ["billOfLadingNumber"] = "BLNumber" },
            ContainerFieldMap = new() { ["billOfLadingNumber"] = "BLNumber", ["containerNumber"] = "ContainerNo" }
        };
        var options = new AnalyzeResultMappingOptions { Models = new() { ["m"] = profile } };
        var result = new ModelResultMapper(new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(options))).MapManifest(json, null, "m", .8).Value!;
        var bill = Assert.Single(result.BillsOfLading);
        var container = Assert.Single(bill.Containers);
        Assert.Equal("UACU5636100", container.Fields["containerNumber"].Value);
        Assert.Equal("UACU  5636100", container.Fields["containerNumber"].RawText);
        Assert.Equal(.99, container.Fields["containerNumber"].Confidence);
        Assert.Equal(1, container.Fields["containerNumber"].Page);
        Assert.Equal("blNumber", container.AssociationMethod);
        Assert.Empty(result.UnassignedContainers);
        Assert.DoesNotContain(result.Flags, f => f.Reason is FlagReasons.CheckDigit or FlagReasons.OrphanContainer);
    }

    internal static AnalyzeResultMappingOptions Options()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build();
        return configuration.GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!;
    }
    private static string Fixture(string name, string suffix) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", name + suffix));
    private static ExtractedManifest Parse(string name = "test-A-kestrel-seen") => ManifestAnalyzeResultParser.Parse(
        Fixture(name, ".analyze.json"), null, "manifest-v1", Options().Models["manifest-v1"]);
    private static CanonicalManifestResult Map(ExtractedManifest input, double threshold = .8) => MappingServices.Manifest.Map(input, threshold).Value!;

    
    [TestCase("test-A-kestrel-seen", 9, "2,4,3", 0)]
    [TestCase("test-B-sahel-unseen", 5, "3,2", 2)]
    public void Real_fixtures_match_every_truth_association(string name, int count, string sizes, int dropped)
    {
        var input = Parse(name);
        var result = new ModelResultMapper(new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(Options())))
            .MapManifest(Fixture(name, ".analyze.json"), null, "manifest-v1", .8).Value!;
        using var truth = JsonDocument.Parse(Fixture(name, ".truth.json"));
        var expected = truth.RootElement.GetProperty("Containers").EnumerateArray()
            .ToDictionary(c => c.GetProperty("ContainerNo").GetString()!, c => c.GetProperty("BLNumber").GetString()!);
        var actual = result.BillsOfLading.SelectMany(b => b.Containers.Select(c =>
            (Number: c.Fields["containerNumber"].Value!, Bill: b.Fields["billOfLadingNumber"].Value!))).ToDictionary(c => c.Number, c => c.Bill);
        Assert.DoesNotContain(result.Flags, f => f.Reason is FlagReasons.TotalsMismatch or FlagReasons.TotalsUnverified);
        Assert.All(input.BillsOfLading.Where(b => new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(b).Length > 0), b => Assert.DoesNotContain(b.Fields.Keys, k => k is "containerCount" or "packages" or "grossWeight"));
        Assert.Equal(count, actual.Count);
        foreach (var (number, bill) in expected) Assert.Equal(bill, actual[number]);
        Assert.Equal(sizes, string.Join(",", result.BillsOfLading.Select(b => b.Containers.Length)));
        Assert.Empty(result.UnassignedContainers);
        Assert.Equal(dropped, result.Flags.Count(f => f.Reason == "BL_ROW_DROPPED"));
        Assert.Equal(dropped, result.UnidentifiedBillsOfLading.Length);
        if (name == "test-B-sahel-unseen")
            Assert.Contains(result.UnidentifiedBillsOfLading, b => b.Fields.GetValueOrDefault("shipperName")?.Value == "DELTA PRODUCE LOGISTICS S.A.E.");
        for (var i = 0; i < result.UnidentifiedBillsOfLading.Length; i++)
        {
            var row = result.UnidentifiedBillsOfLading[i];
            Assert.Equal(input.BillsOfLading.Single(b => b.SourceIndex == row.SourceIndex).Fields, row.Fields);
            Assert.Contains(result.Flags, f => f.Field == $"unidentifiedBillsOfLading[{i}]" && f.Reason == "BL_ROW_DROPPED");
        }
        Assert.Equal(input.Containers.Length, result.BillsOfLading.Sum(b => b.Containers.Length));
        Assert.All(input.Containers, r => Assert.NotEmpty(r.Geometry.Values.SelectMany(v => v)));
        Assert.Equal(Enumerable.Range(0, input.BillsOfLading.Length), input.BillsOfLading.Select(b => b.SourceIndex));
    }

    
    [TestCase("manifest-v1", "test-A-kestrel-seen", 1)]
    [TestCase("manifest-v2", "test-A-kestrel-seen.v2", 2)]
    public void Wrapped_names_merge_and_match_truth(string model, string fixture, int mergedCount)
    {
        var input = ManifestAnalyzeResultParser.Parse(Fixture(fixture, ".analyze.json"), null, model, Options().Models[model]);
        var result = Map(input);
        using var truth = JsonDocument.Parse(Fixture("test-A-kestrel-seen", ".truth.json"));
        Assert.Equal(3, result.BillsOfLading.Length);
        Assert.Empty(result.UnidentifiedBillsOfLading);
        Assert.DoesNotContain(result.Flags, f => f.Reason == FlagReasons.BlRowDropped);
        Assert.Equal(mergedCount, result.Flags.Count(f => f.Reason == FlagReasons.RowMerged && !f.Field.Contains(".fields.")));
        for (var i = 0; i < 3; i++)
        {
            var expected = truth.RootElement.GetProperty("BillsOfLading")[i];
            var fields = result.BillsOfLading[i].Fields;
            Assert.Equal(expected.GetProperty("Shipper").GetString()!.Split('\n')[0], fields["shipperName"].Value);
            Assert.Equal(expected.GetProperty("Consignee").GetString()!.Split('\n')[0], fields["consigneeName"].Value);
            Assert.Equal(expected.GetProperty("BookingNumber").GetString(), fields["bookingNumber"].Value);
            Assert.Equal(expected.GetProperty("FreightTerms").GetString(), fields["freightTerms"].Value);
        }
        foreach (var index in model == "manifest-v2" ? new[] { 0, 2 } : new[] { 0 })
            foreach (var field in new[] { "shipperName", "consigneeName", "bookingNumber", "freightTerms" })
                Assert.Contains(result.Flags, f => f.Field == $"billsOfLading[{index}].fields.{field}" && f.Reason == FlagReasons.RowMerged);
        Assert.Equal("2,4,3", string.Join(",", result.BillsOfLading.Select(b => b.Containers.Length)));
        Assert.Empty(result.UnassignedContainers);
        var expectedOwners = truth.RootElement.GetProperty("Containers").EnumerateArray()
            .ToDictionary(c => c.GetProperty("ContainerNo").GetString()!, c => c.GetProperty("BLNumber").GetString());
        foreach (var bill in result.BillsOfLading)
            foreach (var container in bill.Containers)
                Assert.Equal(expectedOwners[container.Fields["containerNumber"].Value!], bill.Fields["billOfLadingNumber"].Value);
        var merged = new ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService.ManifestRowMergePolicyService(new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService()).Merge(input.BillsOfLading, input.PageCount).Rows[0];
        var first = input.BillsOfLading[0].Fields["shipperName"];
        var next = input.BillsOfLading[1].Fields["shipperName"];
        Assert.Equal(Math.Min(first.Confidence, next.Confidence), merged.Fields["shipperName"].Confidence);
        Assert.Null(merged.Fields["shipperName"].RawText);
        Assert.Equal(2, merged.Geometry["shipperName"].Length);
        Assert.Equal(first.Page, merged.Fields["shipperName"].Page);
        Assert.Contains(Map(input, 1).Flags, f => f.Field == "billsOfLading[0].fields.shipperName" && f.Reason == FlagReasons.LowConfidence);
    }

    [Test]
    public void Sidebar_layout_merges_only_truth_consistent_second_bill_fields()
    {
        var result = Map(Parse("test-B-sahel-unseen"));
        using var truth = JsonDocument.Parse(Fixture("test-B-sahel-unseen", ".truth.json"));
        Assert.Equal(2, result.UnidentifiedBillsOfLading.Length);
        Assert.Equal(new[] { 0, 2 }, result.UnidentifiedBillsOfLading.Select(b => b.SourceIndex));
        Assert.Single(result.BillsOfLading[0].Fields);
        foreach (var pair in new[] { ("consigneeName", "Consignee"), ("notifyParty", "NotifyParty"), ("bookingNumber", "BookingNumber"), ("grossWeight", "GrossWeight") })
            Assert.Equal(truth.RootElement.GetProperty("BillsOfLading")[1].GetProperty(pair.Item2).GetString()!.Split('\n')[0], result.BillsOfLading[1].Fields[pair.Item1].Value);
        Assert.False(result.BillsOfLading[0].Fields.ContainsKey("shipperName"));
        Assert.False(result.BillsOfLading[1].Fields.ContainsKey("shipperName"));
    }

    [Test]
    public void Manifest_v2_maps_all_new_fields()
    {
        var profile = Options().Models["manifest-v2"];
        object Field(string value) => new { valueString = value, confidence = .99 };
        var fields = profile.HeaderFieldMap.ToDictionary(p => p.Value, p => Field(p.Value));
        var bill = new[] { "BLNumber", "ShipperName", "ShipperAddress", "ConsigneeName", "ConsigneeAddress",
            "NotifyParty", "BookingNumber", "FreightTerms", "Packages", "GrossWeight", "Description",
            "ContainerCount", "HSCode", "PackageType", "IssueDate" }.ToDictionary(name => name, name => Field(name));
        fields.Add("BillsOfLading", new { valueArray = new[] { new { valueObject = bill } } });
        var json = JsonSerializer.Serialize(new { pages = new[] { new { pageNumber = 1 } }, documents = new[] { new { fields } } });
        var parsed = ManifestAnalyzeResultParser.Parse(json, null, "manifest-v2", profile);
        Assert.Equal("PlaceOfDelivery", parsed.Header["placeOfDelivery"].Value);
        foreach (var name in new[] { "shipperName", "shipperAddress", "consigneeName", "consigneeAddress", "containerCount", "hsCode", "packageType", "issueDate" })
            Assert.Equal(profile.BillFieldMap[name], Assert.Single(parsed.BillsOfLading).Fields[name].Value);
    }

    [Test]
    public void Explicit_reference_trims_and_ignores_case_and_wins_over_position()
    {
        var d = Parse();
        var bills = new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Sort(d.BillsOfLading.Where(b => new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(b).Length > 0), d.PageCount);
        var reference = " " + new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(bills[2]).ToLowerInvariant() + " ";
        var c = d.Containers[0] with { Fields = d.Containers[0].Fields.SetItem("billOfLadingNumber", new(reference, null, .9, 1)) };
        var r = Map(d with { Containers = [c] });
        Assert.Empty(r.UnassignedContainers);
        Assert.Equal("blNumber", Assert.Single(r.BillsOfLading[2].Containers).AssociationMethod);
        Assert.Equal(reference, r.BillsOfLading[2].Containers[0].Fields["billOfLadingNumber"].Value);
    }

    
    [TestCase(false, "BL_REFERENCE_NOT_FOUND")]
    [TestCase(true, "AMBIGUOUS_BL_REFERENCE")]
    public void Unknown_or_duplicate_printed_reference_never_uses_position(bool duplicate, string reason)
    {
        var d = Parse(); var first = d.BillsOfLading.First(b => new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(b).Length > 0);
        var c = d.Containers[0] with { Fields = d.Containers[0].Fields.SetItem("billOfLadingNumber", new(duplicate ? new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(first) : "UNKNOWN", null, .9, 1)) };
        if (duplicate) d = d with { BillsOfLading = d.BillsOfLading.Add(first with { SourceIndex = 100 }) };
        var r = Map(d with { Containers = [c] });
        Assert.Single(r.UnassignedContainers);
        Assert.Contains(r.Flags, f => f.Field == "unassignedContainers[0]" && f.Reason == reason);
    }

    [Test]
    public void Reordered_arrays_and_dictionary_insertion_produce_identical_output()
    {
        var d = Parse();
        var changed = d with { BillsOfLading = d.BillsOfLading.Reverse().ToImmutableArray(),
            Containers = d.Containers.Reverse().Select(r => r with { Fields = r.Fields.Reverse().ToImmutableDictionary() }).ToImmutableArray() };
        var a = JsonSerializer.Serialize(ModelResultMapper.ToContract(Map(d)), ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options);
        var b = JsonSerializer.Serialize(ModelResultMapper.ToContract(Map(changed)), ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options);
        Assert.Equal(a, b);
    }

    private static ManifestRow At(ManifestRow row, int page, double y) => row with
    { Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty.Add("cell", [new(page, [0, y, 1, y, 1, y + .1, 0, y + .1])]) };

    
    [TestCase("before", "ORPHAN_CONTAINER")]
    [TestCase("missing", "ASSOCIATION_POSITION_MISSING")]
    [TestCase("ties", "AMBIGUOUS_BL_REFERENCE")]
    public void Unsafe_position_is_retained_unassigned(string mutation, string reason)
    {
        var d = Parse();
        var bills = d.BillsOfLading.Where(b => new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(b).Length > 0).ToImmutableArray();
        var c = At(d.Containers[0], 1, 0);
        if (mutation == "missing") c = c with { Geometry = ImmutableDictionary<string, ImmutableArray<CellRegion>>.Empty };
        if (mutation == "ties") { bills = bills.Select(b => At(b, 1, 0)).ToImmutableArray(); c = At(c, 1, 1); }
        var r = Map(d with { BillsOfLading = bills, Containers = [c] });
        Assert.Single(r.UnassignedContainers);
        Assert.Contains(r.Flags, f => f.Reason == reason && f.Field == "unassignedContainers[0]");
        Assert.Null(r.UnassignedContainers[0].AssociationMethod);
    }

    [Test]
    public void Continuation_on_next_page_stays_with_previous_bill_until_next_anchor()
    {
        var d = Parse(); var bills = d.BillsOfLading.Where(b => new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Identifier(b).Length > 0).Take(2).ToArray();
        var r = Map(d with { BillsOfLading = [At(bills[0], 1, 5), At(bills[1], 2, 3)], Containers = [At(d.Containers[0], 2, 1)] });
        Assert.Single(r.BillsOfLading[0].Containers); Assert.Empty(r.BillsOfLading[1].Containers);
    }

    [Test]
    public void All_found_fields_get_confidence_flags_and_numbers_get_iso_check()
    {
        var d = Parse(); var r = Map(d, 1);
        var fields = r.Header.Count + r.BillsOfLading.Sum(b => b.Fields.Count + b.Containers.Sum(c => c.Fields.Count))
            + r.UnidentifiedBillsOfLading.Sum(b => b.Fields.Count);
        Assert.Equal(fields, r.Flags.Count(f => f.Reason == "LOW_CONFIDENCE"));
        Assert.Contains(r.Flags, f => f.Field == "header.carrierName" && f.Reason == "LOW_CONFIDENCE");
        var invalid = d.Containers[0] with { Fields = d.Containers[0].Fields.SetItem("containerNumber", new("INVALID", null, .1, 1)) };
        var invalidResult = Map(d with { Containers = [invalid] });
        Assert.Contains(invalidResult.Flags, f => f.Field == "billsOfLading[0].containers[0].containerNumber" && f.Reason == "CHECK_DIGIT");
        Assert.Contains(invalidResult.Flags, f => f.Field == "billsOfLading[0].containers[0].containerNumber" && f.Reason == "LOW_CONFIDENCE");
    }

    [Test]
    public void Partial_container_and_missing_geometry_survive_parser_without_invented_position()
    {
        var json = JsonNode.Parse(Fixture("test-A-kestrel-seen", ".analyze.json"))!;
        var row = json["analyzeResult"]!["documents"]![0]!["fields"]!["Containers"]!["valueArray"]![0]!["valueObject"]!.AsObject();
        row.Remove("ContainerNo");
        foreach (var field in row) field.Value!.AsObject().Remove("boundingRegions");
        var d = ManifestAnalyzeResultParser.Parse(json.ToJsonString(), null, "manifest-v1", Options().Models["manifest-v1"]);
        Assert.Equal(9, d.Containers.Length);
        Assert.Null(new ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService.ManifestAssociationPolicyService().Anchor(d.Containers[0], 2));
        Assert.Single(Map(d).UnassignedContainers);
    }

    [Test]
    public void Producing_BL_model_maps_all_additive_fields_without_normalization()
    {
        var options = Options(); var profile = options.Models["bl-maersk-v1"];
        object Field(string value) => new { valueString = value, content = value, confidence = .5 };
        var fields = profile.FieldMap.ToDictionary(p => p.Value, p => Field(p.Value + " 001.00 M/V"));
        var container = profile.ContainerFieldMap.ToDictionary(p => p.Value,
            p => Field(p.Key == "containerNumber" ? "CSQU3054383" : p.Value + " 001.00"));
        fields.Add("Containers", new { valueArray = new[] { new { valueObject = container } } });
        var json = JsonSerializer.Serialize(new { pages = new[] { new { pageNumber = 1 } }, documents = new[] { new { fields } } });
        var parsed = new AnalysisParser(new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(options))).Parse(json, "BL", null, "bl-maersk-v1");
        var result = MappingServices.Bill.Map(parsed, .8).Value!;
        Assert.Equal(15, result.Fields.Count);
        foreach (var (key, name) in profile.FieldMap) Assert.Equal(name + " 001.00 M/V", result.Fields[key].Value);
        var c = Assert.Single(result.Containers);
        Assert.Equal("Type 001.00", c.ContainerType!.Value);
        Assert.Equal("Packages 001.00", c.Packages!.Value);
        Assert.Equal("GrossWeightKg 001.00", c.GrossWeightKg!.Value);
        Assert.Equal("MeasurementCbm 001.00", c.MeasurementCbm!.Value);
        Assert.Equal(21, result.Flags.Count(f => f.Reason == "LOW_CONFIDENCE"));
    }

    [Test]
    public void Missing_or_wrong_manifest_profile_is_configuration_error_and_BL_fallback_is_preserved()
    {
        var options = Options();
        Assert.Same(options, MappingProfiles.Select(options, "legacy", "BL"));
        Assert.Same(options.Models["bl-maersk-v1"], MappingProfiles.Select(options, "bl-maersk-v1", "BL"));
        Assert.Throws<OptionsValidationException>(() => MappingProfiles.Select(options, "bl-maersk-v1", "MANIFEST"));
        Assert.Same(options.Models["manifest-v1"], MappingProfiles.SelectManifest(options, "manifest-v1"));
        Assert.Throws<OptionsValidationException>(() => MappingProfiles.SelectManifest(options, "bl-maersk-v1"));
        options.Models.Remove("manifest-v1");
        Assert.Throws<OptionsValidationException>(() => MappingProfiles.SelectManifest(options, "manifest-v1"));
        Assert.Throws<OptionsValidationException>(() => new ModelResultMapper(new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(options))).MapManifest("{}", null, "manifest-v1", .8));
    }
}
