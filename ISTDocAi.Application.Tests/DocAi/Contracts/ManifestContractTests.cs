using ISTDocAi.Application.Tests.DocAi.Contracts.Support;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using Json.Schema;

namespace ISTDocAi.Application.Tests.DocAi.Contracts;
public sealed partial class ManifestContractTests
{
    [Test]
    public void Optional_manifest_v2_fields_and_totals_flags_validate_and_round_trip()
    {
        var field = new ExtractedField
        {
            Value = "3",
            Confidence = 1,
            Page = 1
        };
        var contract = new CanonicalManifestResult
        {
            ContractVersion = "1.0",
            DocumentTypeCode = "MANIFEST",
            ShippingLineCode = null,
            ModelId = "manifest-v2",
            PageCount = 1,
            Header = new()
            {
                ["placeOfDelivery"] = field
            },
            BillsOfLading = [new(new() { ["containerCount"] = field, ["hsCode"] = field, ["packageType"] = field, ["issueDate"] = field, ["shipperAddress"] = field, ["consigneeAddress"] = field }, [])],
            UnidentifiedBillsOfLading = [],
            UnassignedContainers = [],
            Flags = [new("billsOfLading[0]", FlagReasons.TotalsMismatch), new("billsOfLading[0]", FlagReasons.TotalsUnverified)]
        };
        var json = JsonSerializer.SerializeToNode(contract, ContractJson.Options)!;
        using var stream = typeof(CanonicalManifestResult).Assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-manifest-result-v1.schema.json")!;
        using var reader = new StreamReader(stream);
        Assert.That(JsonSchema.FromText(reader.ReadToEnd()).Evaluate(json).IsValid, Is.True);
        var roundTrip = JsonSerializer.Deserialize(json, ContractJsonContext.Default.CanonicalManifestResult)!;
        Assert.That(JsonNode.DeepEquals(json, JsonSerializer.SerializeToNode(roundTrip, ContractJson.Options)), Is.True);
    }

    [TestCase("header")]
    [TestCase("bill")]
    [TestCase("unidentified")]
    public void Additional_scalar_fields_validate_but_malformed_fields_and_structure_do_not(string scope)
    {
        var field = new ExtractedField
        {
            Value = "extra value",
            Confidence = .9,
            Page = 1
        };
        var contract = new CanonicalManifestResult
        {
            ContractVersion = "1.0",
            DocumentTypeCode = "MANIFEST",
            ShippingLineCode = null,
            ModelId = "manifest-v1",
            PageCount = 1,
            Header = new(),
            BillsOfLading = [new(new(), [new() { ContainerNumber = field }])],
            UnidentifiedBillsOfLading = [new(0, new())],
            UnassignedContainers = [],
            Flags = []
        };
        var json = JsonSerializer.SerializeToNode(contract, ContractJson.Options)!;
        var fields = scope switch
        {
            "header" => json["header"]!,
            "bill" => json["billsOfLading"]![0]!["fields"]!,
            _ => json["unidentifiedBillsOfLading"]![0]!["fields"]!
        };
        fields["extraScalar"] = JsonSerializer.SerializeToNode(field, ContractJson.Options);
        using var stream = typeof(CanonicalManifestResult).Assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-manifest-result-v1.schema.json")!;
        using var reader = new StreamReader(stream);
        var schema = JsonSchema.FromText(reader.ReadToEnd());
        Assert.That(schema.Evaluate(json).IsValid, Is.True);
        var roundTrip = JsonSerializer.Deserialize(json, ContractJsonContext.Default.CanonicalManifestResult)!;
        Assert.That(roundTrip.ContractVersion, Is.EqualTo("1.0"));
        Assert.That(JsonNode.DeepEquals(json, JsonSerializer.SerializeToNode(roundTrip, ContractJson.Options)), Is.True);
        var valid = fields["extraScalar"]!.DeepClone();
        foreach (var malformed in new JsonNode? []
        {
            null,
            JsonValue.Create("text"),
            new JsonArray(),
            new JsonObject
            {
                ["value"] = "missing confidence/page"
            },
            new JsonObject
            {
                ["value"] = new JsonObject(),
                ["confidence"] = .9,
                ["page"] = 1
            },
            new JsonObject
            {
                ["value"] = "x",
                ["confidence"] = 2,
                ["page"] = 1
            },
            new JsonObject
            {
                ["value"] = "x",
                ["confidence"] = .9,
                ["page"] = 0
            }
        }

        )
        {
            fields["extraScalar"] = malformed;
            Assert.That(schema.Evaluate(json).IsValid, Is.False);
        }

        fields["extraScalar"] = valid;
        foreach (var structural in new[]
        {
            json,
            json["billsOfLading"]![0]!,
            json["unidentifiedBillsOfLading"]![0]!,
            json["billsOfLading"]![0]!["containers"]![0]!
        }

        )
        {
            structural["unexpectedProperty"] = JsonValue.Create("x");
            Assert.That(schema.Evaluate(json).IsValid, Is.False);
            structural.AsObject().Remove("unexpectedProperty");
            Assert.That(schema.Evaluate(json).IsValid, Is.True);
        }
    }

    [Test]
    public void Old_BL_result_round_trips_without_new_members()
    {
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", "webhook-job-completed.json")))!["result"]!;
        var result = JsonSerializer.Deserialize(original, ContractJsonContext.Default.CanonicalResult)!;
        Assert.That(JsonNode.DeepEquals(original, JsonSerializer.SerializeToNode(result, ContractJson.Options)), Is.True);
    }

    [Test]
    public void Every_new_BL_container_member_round_trips()
    {
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", "webhook-job-completed.json")))!["result"]!;
        foreach (var name in new[]
        {
            "containerType",
            "packages",
            "grossWeightKg",
            "measurementCbm"
        }

        )
            original["containers"]![0]![name] = new JsonObject
            {
                ["value"] = "001.00",
                ["confidence"] = .6,
                ["page"] = 1
            };
        foreach (var name in new[]
        {
            "bookingNumber",
            "shipperAddress",
            "consigneeAddress",
            "notifyParty",
            "freightTerms",
            "hsCode",
            "totalGrossWeightKg"
        }

        )
            original["fields"]![name] = new JsonObject
            {
                ["value"] = "001.00",
                ["confidence"] = .6,
                ["page"] = 1
            };
        var result = JsonSerializer.Deserialize(original, ContractJsonContext.Default.CanonicalResult)!;
        Assert.That(JsonNode.DeepEquals(original, JsonSerializer.SerializeToNode(result, ContractJson.Options)), Is.True);
        using var stream = typeof(CanonicalResult).Assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-result-v1.schema.json")!;
        using var reader = new StreamReader(stream);
        Assert.That(JsonSchema.FromText(reader.ReadToEnd()).Evaluate(original).IsValid, Is.True);
    }
}
