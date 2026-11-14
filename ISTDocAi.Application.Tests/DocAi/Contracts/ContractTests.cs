using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Tests.DocAi.Contracts.Support;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using Json.Schema;

namespace ISTDocAi.Application.Tests.DocAi.Contracts;
public class ContractTests
{
    private static readonly byte[] Secret = Convert.FromBase64String("ZG9jYWktdGVzdC12ZWN0b3Itc2VjcmV0LTMyYnl0ZXM=");
    private static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", name));
    [TestCase(1, "1793268922", "4dad37deda733dbf91e6907cd9b718ef45ae55cd6b44a4f382e32258f3d31e14", 326)]
    [TestCase(2, "1793271763", "4489eab28971b0bd17ccc6b66e0e2eba5b12b33c7ddc9d690686afbad068b168", 346)]
    public void SignatureVectors(int vector, string timestamp, string hex, int length)
    {
        byte[] body = Sample($"hmac-vector-{vector}-body.json");
        Assert.That(body.Length, Is.EqualTo(length));
        string signature = "sha256=" + hex;
        Assert.That(WebhookSignature.Compute(Secret, timestamp, body), Is.EqualTo(signature));
        Assert.That(WebhookSignature.Verify(timestamp, body, signature, Secret), Is.True);
        Assert.That(WebhookSignature.Verify(timestamp, body, signature, new byte[32], Secret), Is.True);
        Assert.That(WebhookSignature.Verify(timestamp, body, signature, new byte[32]), Is.False);
        Assert.That(WebhookSignature.Verify((long.Parse(timestamp) + 1).ToString(), body, signature, Secret), Is.False);
        body[0] ^= 1;
        Assert.That(WebhookSignature.Verify(timestamp, body, signature, Secret), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("-1")]
    [TestCase("9223372036854775807")]
    [TestCase("999999999999")]
    [TestCase("1793268621")]
    [TestCase("1793269223")]
    [TestCase("not-a-time")]
    public void FreshnessRejectsInvalidOldAndFuture(string? timestamp) => Assert.That(WebhookSignature.IsFresh(timestamp, DateTimeOffset.FromUnixTimeSeconds(1793268922), TimeSpan.FromMinutes(5)), Is.False);
    [TestCase("1793268622")]
    [TestCase("1793268922")]
    [TestCase("1793269222")]
    public void FreshnessAcceptsInclusiveWindow(string timestamp) => Assert.That(WebhookSignature.IsFresh(timestamp, DateTimeOffset.FromUnixTimeSeconds(1793268922), TimeSpan.FromMinutes(5)), Is.True);
    [TestCase(null, null)]
    [TestCase("", "sha256=")]
    [TestCase("\uFF11\uFF12\uFF13", "sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    [TestCase("123", "sha256=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [TestCase("123", "sha256=000000000000000000000000000000000000000000000000000000000000000g")]
    public void MalformedHeadersAreRejected(string? timestamp, string? signature)
    {
        Assert.That(WebhookSignature.LooksValid(timestamp, signature), Is.False);
        Assert.That(WebhookSignature.Verify(timestamp, [], signature, Secret), Is.False);
    }

    [Test]
    public void CompletedWebhookPreservesEverySampleMember()
    {
        byte[] json = Sample("webhook-job-completed.json");
        var value = JsonSerializer.Deserialize<WebhookEnvelope>(json, ContractJson.Options)!;
        Assert.That(value.RunNo, Is.EqualTo(1));
        Assert.That(value.EventType, Is.EqualTo(EventTypes.JobCompleted));
        var bill = PortAssertions.IsType<CanonicalResult>(value.Result);
        Assert.That(bill.Fields.Count, Is.EqualTo(8));
        Assert.That(bill.Fields[FieldNames.ShipperName].RawText, Is.EqualTo("ACME EXPORTS LTD."));
        var original = JsonNode.Parse(json)!;
        Assert.That(value.OccurredAtUtc, Is.EqualTo(DateTimeOffset.Parse(original["occurredAtUtc"]!.GetValue<string>())));
        // DateTimeOffset preserves the instant; STJ writes UTC as +00:00 rather than Z.
        original["occurredAtUtc"] = value.OccurredAtUtc;
        original["requestedDocumentType"] = null;
        original["documentType"] = null;
        original["resultType"] = null;
        original["runReason"] = "New";
        AssertEquivalent(original, JsonSerializer.SerializeToNode(value, ContractJson.Options));
    }

    [Test]
    public void CatalogPreservesEverySampleMember()
    {
        byte[] json = Sample("catalog-response.json");
        var value = JsonSerializer.Deserialize<CatalogResponse>(json, ContractJson.Options)!;
        Assert.That(value.Items[0].ShippingLineCode, Is.Null);
        Assert.That(value.Items[0].MinFieldConfidence, Is.EqualTo(0.8));
        AssertEquivalent(JsonNode.Parse(json), JsonSerializer.SerializeToNode(value, ContractJson.Options));
    }

    [Test]
    public void CompletedCanonicalResultValidatesAgainstEmbeddedSchema()
    {
        var assembly = typeof(CanonicalResult).Assembly;
        using var stream = assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-result-v1.schema.json")!;
        Assert.That(stream, Is.Not.Null);
        using var reader = new StreamReader(stream);
        var schema = JsonSchema.FromText(reader.ReadToEnd());
        var result = JsonNode.Parse(Sample("webhook-job-completed.json"))!["result"]!;
        Assert.That(schema.Evaluate(result).IsValid, Is.True);
        result["pageCount"] = 0;
        Assert.That(schema.Evaluate(result).IsValid, Is.False);
        result["pageCount"] = 2;
        result["fields"]!["shipperName"]!["confidence"] = 1.1;
        Assert.That(schema.Evaluate(result).IsValid, Is.False);
    }

    [Test]
    public void MinorVersionWithAdditiveFieldsValidatesAndDeserializes()
    {
        var result = JsonNode.Parse(Sample("webhook-job-completed.json"))!["result"]!;
        result["contractVersion"] = "1.1";
        result["fields"]!["notifyPartyName"] = new JsonObject
        {
            ["value"] = "NOTIFY COMPANY",
            ["confidence"] = 0.92,
            ["page"] = 1
        };
        result["containers"]![0]!["containerType"] = new JsonObject
        {
            ["value"] = "40HC",
            ["confidence"] = 0.95,
            ["page"] = 1
        };
        Assert.That(LoadCanonicalSchema().Evaluate(result).IsValid, Is.True);
        var value = JsonSerializer.Deserialize<CanonicalResult>(result.ToJsonString(), ContractJson.Options)!;
        Assert.That(value.ContractVersion, Is.EqualTo("1.1"));
        Assert.That(value.Fields["notifyPartyName"].Value, Is.EqualTo("NOTIFY COMPANY"));
        Assert.That(value.Containers[0].ContainerNumber.Value, Is.EqualTo("MSKU1234565"));
        Assert.That(value.Containers[0].SealNumber, Is.Not.Null);
    }

    [Test]
    public void MissingSealValidatesAndRemainsOmittedOnSerialization()
    {
        var result = JsonNode.Parse(Sample("webhook-job-completed.json"))!["result"]!;
        Assert.That(result["containers"]![0]!.AsObject().Remove("sealNumber"), Is.True);
        // The seal flag is irrelevant when the seal was not detected.
        result["flags"] = new JsonArray();
        Assert.That(LoadCanonicalSchema().Evaluate(result).IsValid, Is.True);
        var value = JsonSerializer.Deserialize<CanonicalResult>(result.ToJsonString(), ContractJson.Options)!;
        Assert.That(value.Containers[0].SealNumber, Is.Null);
        var serialized = JsonSerializer.SerializeToNode(value, ContractJson.Options)!;
        Assert.That(serialized["containers"]![0]!.AsObject().ContainsKey("sealNumber"), Is.False);
        Assert.That(LoadCanonicalSchema().Evaluate(serialized).IsValid, Is.True);
    }

    [TestCase("2.0")]
    [TestCase("1")]
    [TestCase("1.x")]
    public void OtherMajorOrMalformedVersionsAreRejected(string version)
    {
        var result = JsonNode.Parse(Sample("webhook-job-completed.json"))!["result"]!;
        result["contractVersion"] = version;
        Assert.That(LoadCanonicalSchema().Evaluate(result).IsValid, Is.False);
    }

    private static JsonSchema LoadCanonicalSchema()
    {
        using var stream = typeof(CanonicalResult).Assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-result-v1.schema.json")!;
        Assert.That(stream, Is.Not.Null);
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    [Test]
    public void PostAndGetSerializeWithCamelCaseAndNullOutcome()
    {
        var post = new SubmitJobResponse
        {
            JobId = Guid.NewGuid(),
            RunNo = 1,
            Status = RunStatuses.Queued,
            StatusUrl = "/api/v1/jobs/id?runNo=1",
            PageCount = 2
        };
        var node = JsonSerializer.SerializeToNode(post, ContractJson.Options)!.AsObject();
        Assert.That(node.Select(p => p.Key), Is.EqualTo(new[] { "jobId", "runNo", "runReason", "status", "statusUrl", "pageCount" }));
        var get = new JobResponse
        {
            JobId = post.JobId,
            RunNo = 1,
            ClientRequestId = "request-1",
            Status = RunStatuses.Queued,
            Error = null,
            Result = null
        };
        Assert.That(JsonSerializer.Deserialize<JobResponse>(JsonSerializer.Serialize(get, ContractJson.Options), ContractJson.Options), Is.EqualTo(get));
    }

    // Compare recursively: every input member must survive with its value and wire name.
    private static void AssertEquivalent(JsonNode? expected, JsonNode? actual)
    {
        if (expected is JsonObject obj)
        {
            var target = PortAssertions.IsType<JsonObject>(actual);
            Assert.That(target.Count, Is.EqualTo(obj.Count));
            foreach (var pair in obj)
            {
                Assert.That(target.ContainsKey(pair.Key), Is.True, pair.Key);
                AssertEquivalent(pair.Value, target[pair.Key]);
            }
        }
        else if (expected is JsonArray array)
        {
            var target = PortAssertions.IsType<JsonArray>(actual);
            Assert.That(target.Count, Is.EqualTo(array.Count));
            for (int i = 0; i < array.Count; i++)
                AssertEquivalent(array[i], target[i]);
        }
        else
            Assert.That(JsonNode.DeepEquals(expected, actual), Is.True, $"Expected {expected}, got {actual}");
    }
}
