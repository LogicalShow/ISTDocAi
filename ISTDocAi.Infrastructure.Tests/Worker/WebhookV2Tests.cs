using ISTDocAi.Application;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

[SetUICulture("en-US")]
public sealed class WebhookV2Tests
{
    private static readonly Guid Job = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Event = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Lease = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static async Task<WebhookFakes> Scenario(string name)
    {
        var f = name is "manifest" or "unclassified" ? AutoManifestWorkerTests.Auto("MANIFEST", null) : new WorkerFakes();
        f.Job = f.Job with { JobId = Job, LeaseToken = Lease, CorrelationId = "baseline-correlation-1" };
        if (name == "manifest") { f.Job = f.Job with { DocumentTypeCode = "MANIFEST", Stage = "Extract" }; f.Position = f.Position with { Stage = "Extract", RequestedDocumentTypeCode = "MANIFEST", ResolvedDocumentTypeCode = "MANIFEST" }; f.Polls.Dequeue(); }
        if (name == "unclassified") f.Mapping = null;
        if (name == "failed") f.Models.Clear();
        if (name == "model-changed") { f.RunReason = "ModelChanged"; f.ChangesJson = """{"extractModel":{"oldModelId":"bl-v0","newModelId":"bl-v1"},"mapping":true}"""; }
        await f.Processor().ExecuteAsync(f.Job, default);
        var payload = f.Completion?.PayloadJson ?? f.Classification?.PayloadJson ?? f.Failure!.PayloadJson;
        var prepared = Regex.Replace(payload, "(?<=\"eventId\":\")[0-9a-f-]{36}(?=\")", Event.ToString());
        var w = new WebhookFakes();
        w.Delivery = w.Delivery with { JobId = Job, PayloadJson = prepared, EventId = Event };
        w.Context = new("baseline-correlation-1", f.Completion?.ResultJson);
        return w;
    }

    [TestCase("bl")]
    [TestCase("manifest")]
    [TestCase("model-changed")]
    [TestCase("unclassified")]
    public async Task Golden_scenario_projects_every_member_and_preserves_signing_scheme_and_headers(string name)
    {
        var w = await Scenario(name);
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Webhook", name + ".json")));
        var golden = fixture.RootElement;
        var headers = golden.GetProperty("headers").EnumerateArray().ToDictionary(h => h.GetProperty("name").GetString()!, h => h.GetProperty("values").EnumerateArray().Select(v => v.GetString()!).ToArray());
        var v1 = Convert.FromBase64String(golden.GetProperty("bodyBase64").GetString()!);
        Assert.That(w.Key, Is.EqualTo(Convert.FromBase64String(golden.GetProperty("fakeSecretBase64").GetString()!)));
        Assert.That(w.Clock.GetUtcNow().ToUnixTimeSeconds().ToString(), Is.EqualTo(headers["X-Timestamp"][0]));
        Assert.That(WebhookSignature.Compute(w.Key, headers["X-Timestamp"][0], v1), Is.EqualTo(headers["X-Signature"][0]));
        var expected = ProjectGolden(JsonNode.Parse(v1)!.AsObject());
        using var capture = new Capture(async message => {
            var body = await message.Content!.ReadAsByteArrayAsync();
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(body), expected), Is.True);
            Assert.That(message.Method.Method, Is.EqualTo(golden.GetProperty("method").GetString()));
            Assert.That(message.RequestUri!.AbsoluteUri, Is.EqualTo(golden.GetProperty("url").GetString()));
            var actual = message.Headers.Concat(message.Content.Headers).ToDictionary(h => h.Key, h => h.Value.ToArray());
            Assert.That(actual.Keys, Is.EquivalentTo(headers.Keys));
            Assert.That(body, Is.EqualTo(DeliverWebhook.BuildBody(w.Delivery.PayloadJson, w.Context.ResultJson, new DocAiV2Json())));
            foreach (var h in headers)
                Assert.That(actual[h.Key], Is.EqualTo(h.Key switch {
                    "X-Signature" => new[] { WebhookSignature.Compute(w.Key, headers["X-Timestamp"][0], body) },
                    "Content-Length" => new[] { body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    _ => h.Value }));
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var transport = new WebhookHttpTransport(new Factory(capture));
        await w.Processor(transport: transport).ExecuteAsync(w.Delivery);
        Assert.That(capture.Calls, Is.EqualTo(1));
        Assert.That(w.Attempts.Single(), Is.EqualTo(((short?)200, (string?)null)));
    }

    // Independent documented v1->v2 mapping, including nested PascalCase metadata.
    private static JsonObject ProjectGolden(JsonObject old)
    {
        var job = new JsonObject();
        foreach (var name in new[] { "jobId", "runNo", "clientRequestId", "runReason", "changes", "status", "error", "result", "requestedDocumentType", "documentType", "resultType" })
        {
            if (name == "changes" && old[name] is null) continue;
            job[char.ToUpperInvariant(name[0]) + name[1..]] = name is "changes" or "error" ? Pascal(old[name]) : old[name]?.DeepClone();
        }
        return new JsonObject { ["EventId"] = old["eventId"]?.DeepClone(), ["EventType"] = old["eventType"]?.DeepClone(), ["OccurredAtUtc"] = old["occurredAtUtc"]?.DeepClone(), ["Job"] = job };
    }
    private static JsonNode? Pascal(JsonNode? value)
    {
        if (value is not JsonObject obj) return value?.DeepClone();
        var result = new JsonObject();
        foreach (var x in obj) result[char.ToUpperInvariant(x.Key[0]) + x.Key[1..]] = Pascal(x.Value);
        return result;
    }

    [TestCase("bl")]
    [TestCase("failed")]
    [TestCase("manifest")]
    [TestCase("model-changed")]
    [TestCase("unclassified")]
    public async Task Canonical_webhook_Job_bytes_equal_GET_Value_for_the_same_consumer_and_stored_run(string name)
    {
        var w = await Scenario(name);
        var payload = JsonNode.Parse(w.Delivery.PayloadJson)!.AsObject();
        var store = new StoredRun(payload, w.Context.ResultJson);
        using var services = new ServiceCollection().AddLogging().AddLocalization().AddApplication().BuildServiceProvider();
        var handler = new GetJobQueryHandler(new GetJob(store), services.GetRequiredService<IMessageLocalizer>());
        var response = await handler.Handle(new(new(1, "TEST", "TEST", [], null, 10, 100, true), Job, 1), default);
        var options = new DocAiV2Json().Options;
        var get = JsonSerializer.SerializeToUtf8Bytes(response, options);
        await w.Processor().ExecuteAsync(w.Delivery);
        using var webhook = JsonDocument.Parse(w.Requests.Single().Body);
        using var envelope = JsonDocument.Parse(get);
        Assert.That(Encoding.UTF8.GetBytes(webhook.RootElement.GetProperty("Job").GetRawText()),
            Is.EqualTo(Encoding.UTF8.GetBytes(envelope.RootElement.GetProperty("Value").GetRawText())));
        Assert.That(store.Reads, Is.EqualTo(1));
        Assert.That(webhook.RootElement.GetProperty("Job").TryGetProperty("Changes", out _), Is.EqualTo(name == "model-changed"));
    }

    [TestCase("{\"value\":42}")]
    [TestCase("{\"privateField\":\"PRIVATE_VALUE\"}")]
    [TestCase(null)]
    public void Legacy_result_is_inserted_without_canonical_decoding(string? raw)
    {
        var options = new DocAiV2Json();
        var body = DeliverWebhook.BuildBody("{\"eventType\":\"job.completed\"}", raw, options);
        var job = JsonNode.Parse(body)!["Job"]!.AsObject();
        Assert.That(JsonNode.DeepEquals(job["Result"], raw is null ? null : JsonNode.Parse(raw)), Is.True);
        Assert.That(job.Select(p => p.Key), Is.EqualTo(new[] { "JobId", "RunNo", "ClientRequestId", "RunReason", "Status", "Error", "Result", "RequestedDocumentType", "DocumentType", "ResultType" }));
    }

    [Test]
    public void Canonical_branch_keeps_original_discriminator_and_document_type_validation()
    {
        var options = new DocAiV2Json();
        Assert.Throws<JsonException>(() => DeliverWebhook.BuildBody("{\"resultType\":\"unknown\"}", null, options));
        Assert.Throws<JsonException>(() => DeliverWebhook.BuildBody("{\"requestedDocumentType\":null}", "{\"value\":42}", options));
        Assert.Throws<InvalidOperationException>(() => DeliverWebhook.BuildBody("[]", null, options));
    }

    private sealed class StoredRun(JsonObject p, string? result) : IJobRepository
    {
        public int Reads;
        public Task<RunOutcome?> GetRunAsync(Guid id, short consumer, short? run, CancellationToken ct)
        {
            Assert.That((id, consumer, run), Is.EqualTo((Job, (short)1, (short?)1)));
            Reads++;
            return Task.FromResult<RunOutcome?>(new(Job, 1, p["clientRequestId"]!.GetValue<string>(), p["status"]!.GetValue<string>(), p["error"]?["code"]?.GetValue<string>(), p["error"]?["message"]?.GetValue<string>(), default, null, 1, "1.0", result, null) {
                RunReason = p["runReason"]!.GetValue<string>(), ChangesJson = p["changes"]?.ToJsonString(),
                RequestedDocumentTypeCode = p["requestedDocumentType"]?.GetValue<string>(), ResolvedDocumentTypeCode = p["documentType"]?.GetValue<string>(), ResultType = p["resultType"]?.GetValue<string>() });
        }
        public Task<int> DailyPagesAsync(short c, CancellationToken ct) => throw new NotSupportedException();
        public Task<IntakeResult> IntakeAsync(IntakeRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<RunPosition?> GetPositionAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> CompleteAsync(Completion r, CancellationToken ct) => throw new NotSupportedException();
        public Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> FailAsync(Failure r, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess r, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClassMapping?> GetClassMappingAsync(string m, string c, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Capture(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct) { Calls++; return send(message); }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
}
