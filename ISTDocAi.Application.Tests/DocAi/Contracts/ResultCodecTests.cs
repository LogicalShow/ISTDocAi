using ISTDocAi.Application.Tests.DocAi.Contracts.Support;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;

namespace ISTDocAi.Application.Tests.DocAi.Contracts;
public sealed class ResultCodecTests
{
    [Test]
    public void Existing_BL_result_bytes_stay_identical_through_codec_and_envelope_serialization()
    {
        var sample = JsonNode.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", "webhook-job-completed.json")))!;
        var old = JsonSerializer.Deserialize(sample["result"]!.ToJsonString(), ContractJsonContext.Default.CanonicalResult)!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(old, ContractJsonContext.Default.CanonicalResult);
        var decoded = CanonicalResultCodec.Deserialize(Encoding.UTF8.GetString(bytes), ResultTypes.BillOfLading);
        Assert.That(Encoding.UTF8.GetBytes(CanonicalResultCodec.Serialize(decoded)), Is.EqualTo(bytes));
        var historical = CanonicalResultCodec.Deserialize(Encoding.UTF8.GetString(bytes), null);
        Assert.That(Encoding.UTF8.GetBytes(CanonicalResultCodec.Serialize(historical)), Is.EqualTo(bytes));
        var envelope = new JobResponse
        {
            JobId = Guid.NewGuid(),
            RunNo = 1,
            ClientRequestId = "request",
            Status = "Completed",
            Error = null,
            Result = decoded,
            RequestedDocumentType = "BL",
            DocumentType = "BL",
            ResultType = ResultTypes.BillOfLading
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(envelope, ContractJson.Options));
        Assert.That(Encoding.UTF8.GetBytes(document.RootElement.GetProperty("result").GetRawText()), Is.EqualTo(bytes));
        Assert.That(document.RootElement.GetRawText(), Does.Not.Contain("$type"));
    }

    [TestCase("unknown.v1")]
    [TestCase("")]
    public void Unknown_discriminator_is_rejected_in_codec_and_both_envelopes(string discriminator)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", "webhook-job-completed.json"));
        var sample = JsonNode.Parse(json)!;
        sample["resultType"] = discriminator;
        Assert.Throws<JsonException>(() => CanonicalResultCodec.Deserialize(sample["result"]!.ToJsonString(), discriminator));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(sample.ToJsonString(), ContractJsonContext.Default.JobResponse));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(sample.ToJsonString(), ContractJsonContext.Default.WebhookEnvelope));
    }

    [Test]
    public void Discriminator_and_result_type_must_agree_even_when_discriminator_is_after_result()
    {
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Samples", "webhook-job-completed.json")))!;
        sample["resultType"] = ResultTypes.Manifest;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(sample.ToJsonString(), ContractJsonContext.Default.WebhookEnvelope));
        Assert.Throws<JsonException>(() => CanonicalResultCodec.Deserialize(sample["result"]!.ToJsonString(), ResultTypes.Manifest));
    }
}
