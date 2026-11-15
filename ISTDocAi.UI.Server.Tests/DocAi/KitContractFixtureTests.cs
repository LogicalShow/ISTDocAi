using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Tests.DocAi.Worker;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests.DocAi;

// Opt-in regeneration writes only the approved contract-fixture directory. Normal runs compare bytes.
[SetUICulture("en-US")]
public sealed class KitContractFixtureTests
{
    private static readonly Guid Job = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Event = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private const string RequestId = "33333333-3333-4333-8333-333333333333";
    private const string Correlation = "kit-contract-correlation";
    private static readonly JsonSerializerOptions FixtureJson = new() { WriteIndented = true };
    private static string Root
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("DOCAI_CONTRACT_ROOT");
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
            return root?.FullName
                ?? throw new InvalidOperationException("Cannot locate ISTDocAi.sln above the test output directory.");
        }
    }

    [TestCase("cancel-cancelled")]
    [TestCase("cancel-continuing")]
    [TestCase("cancel-not-found")]
    [TestCase("submit")]
    [TestCase("replay")]
    [TestCase("catalog")]
    [TestCase("job-queued")]
    [TestCase("job-bl")]
    [TestCase("job-render-bl")]
    [TestCase("job-render-manifest")]
    [TestCase("job-manifest-auto")]
    [TestCase("job-manifest-large")]
    [TestCase("job-failed")]
    [TestCase("job-not-found")]
    [TestCase("invalid-request")]
    [TestCase("quota-exhausted")]
    [TestCase("daily-cap")]
    [TestCase("rate-limited")]
    [TestCase("database-not-ready")]
    [TestCase("internal-error")]
    [TestCase("usage")]
    [TestCase("unknown-route")]
    [TestCase("webhook-completed")]
    [TestCase("webhook-failed")]
    [TestCase("webhook-raw-result")]
    public async Task Real_pipeline_bytes_match_shared_fixture(string name)
    {
        var fixture = name.StartsWith("webhook-", StringComparison.Ordinal)
            ? await Webhook(name) : await Http(name);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fixture, FixtureJson) + "\n");
        string file = Path.Combine(Root, "ISTDocAi.UI.Server.Tests", "Fixtures", "ContractFixtures", "v2", name + ".json");
        if (Environment.GetEnvironmentVariable("UPDATE_KIT_CONTRACT_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes);
        }
        Assert.That(await File.ReadAllBytesAsync(file), Is.EqualTo(bytes), name);
    }

    private static async Task<JsonObject> Http(string name)
    {
        using var app = new ApiFactory();
        typeof(FakePorts).GetField("<JobId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app.Ports, Job);
        app.Ports.Models.Add(new("MANIFEST", null, "extract", "manifest-v1", .8m));
        app.Ports.Models.Add(new("AUTO", null, "classify", "router-v1", .8m));
        if (name == "replay") app.Ports.FirstOutcome = IntakeOutcome.Existing;
        if (name == "daily-cap") app.Ports.PagesToday = 100;
        if (name == "rate-limited") app.Tokens = 1;
        if (name == "database-not-ready") app.TestDatabaseGate = new DatabaseReadiness();
        if (name == "internal-error") app.Ports.ThrowCatalog = true;
        if (name == "quota-exhausted")
        {
            const string message = "The page allowance is exhausted or insufficient for this document. Contact IST to renew.";
            app.Ports.IntakeError = new(402, "QUOTA_EXHAUSTED", message, message, requiredPages: 6, availablePages: 4);
        }
        bool cancel = name.StartsWith("cancel-", StringComparison.Ordinal);
        if (cancel && name != "cancel-not-found") app.Ports.CancelResult = new(Job, 1, name == "cancel-cancelled" ? "Cancelled" : "Continuing");
        bool job = name.StartsWith("job-", StringComparison.Ordinal);
        if (job && name != "job-not-found") app.Ports.Run = Run(name, app.Clock.Now);
        using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Culture", "en");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", Correlation);
        if (name == "rate-limited") await client.GetAsync("/api/v2/catalog");
        bool post = new[] { "submit", "replay", "invalid-request", "quota-exhausted", "daily-cap" }.Contains(name);
        string route = post ? "/api/v2/jobs" : job ? $"/api/v2/jobs/{Job}?runNo=1"
            : name == "usage" ? "/api/v2/usage?months=3" : name == "unknown-route" ? "/api/v2/unknown" : "/api/v2/catalog";
        using var request = new HttpRequestMessage(post || cancel ? HttpMethod.Post : HttpMethod.Get, cancel ? $"/api/v2/jobs/cancel?requestId={RequestId}" : route);
        if (post) request.Content = HttpGoldenTests.Form(name == "invalid-request" ? [] :
            await File.ReadAllBytesAsync(Path.Combine(Root, "ISTDocAi.UI.Server.Tests", "Fixtures", "Stage6", "baseline.pdf")), requestId: RequestId);
        using var response = await client.SendAsync(request);
        var headers = new JsonObject();
        foreach (var h in response.Headers.Concat(response.Content.Headers).OrderBy(h => h.Key, StringComparer.Ordinal))
            headers[h.Key] = string.Join(", ", h.Value);
        string body = await response.Content.ReadAsStringAsync();
        var fixture = new JsonObject { ["status"] = (int)response.StatusCode, ["headers"] = headers, ["bodyText"] = body };
        if (job && response.IsSuccessStatusCode)
        {
            var old = await new GetJob(app.Ports).ExecuteAsync(Job, app.Ports.Consumer, 1, default);
            fixture["internalText"] = JsonSerializer.Serialize(old, ContractJsonContext.Default.JobResponse);
            using var parsed = JsonDocument.Parse(body);
            Assert.That(parsed.RootElement.GetProperty("Value").GetProperty("Result").GetRawText(),
                Is.EqualTo(JsonDocument.Parse(fixture["internalText"]!.GetValue<string>()).RootElement.GetProperty("result").GetRawText()));
        }
        ReadSurfaceTests.AssertNoPayloadLogs(app);
        return fixture;
    }

    private static RunOutcome Run(string name, DateTimeOffset now)
    {
        bool failed = name == "job-failed", queued = name == "job-queued";
        bool manifest = name.Contains("manifest", StringComparison.Ordinal);
        string? result = failed || queued ? null : name.StartsWith("job-render-", StringComparison.Ordinal)
            // Byte-identical copies of the kit's Forwarding.MvcApp/DocAi/Fixtures results; the kit repo is not
            // checked out next to ISTDocAi (CI, or a clone in another folder).
            ? File.ReadAllText(Path.Combine(Root, "ISTDocAi.UI.Server.Tests", "Fixtures", "KitResults",
                manifest ? "manifest-test-A.json" : "bl.json"), Encoding.UTF8)
            : Result(manifest, name == "job-manifest-large");
        return new(Job, 1, RequestId, failed ? "Failed" : queued ? "Queued" : "Completed",
            failed ? "ANALYSIS_FAILED" : null, failed ? "Saved English failure message" : null,
            now, queued ? null : now, 1, result is null ? null : "1.0", result, null)
        {
            RequestedDocumentTypeCode = manifest ? "AUTO" : "BL",
            ResolvedDocumentTypeCode = result is null ? null : manifest ? "MANIFEST" : "BL",
            ResultType = result is null ? null : manifest ? "cargo-manifest.v1" : "bill-of-lading.v1",
            RunReason = "ModelChanged",
            ChangesJson = """{"classifier":{"oldModelId":"router-v0","newModelId":"router-v1"},"extractModel":{"oldModelId":"v0","newModelId":"v1"},"mapping":true}"""
        };
    }

    private static string Result(bool manifest, bool large)
    {
        var field = new ExtractedField { Value = "قيمة عربية <>& \\" + (large ? new string('x', 1100000) : ""), Confidence = .850, Page = 1 };
        ICanonicalResult result = manifest ? new CanonicalManifestResult {
            ContractVersion = "1.0", DocumentTypeCode = "MANIFEST", ShippingLineCode = null, ModelId = "manifest-v1", PageCount = 2,
            Header = new() { ["carrierName"] = field }, BillsOfLading = [], UnidentifiedBillsOfLading = [], UnassignedContainers = [], Flags = []
        } : new CanonicalResult {
            ContractVersion = "1.0", DocumentTypeCode = "BL", ShippingLineCode = null, ModelId = "bl-v1", PageCount = 1,
            Fields = new() { ["shipperName"] = field }, Containers = [], Flags = []
        };
        return CanonicalResultCodec.Serialize(result);
    }

    private static async Task<JsonObject> Webhook(string name)
    {
        var f = new WebhookFakes();
        var run = Run(name == "webhook-failed" ? "job-failed" : "job-bl", f.Clock.GetUtcNow());
        var ports = new FakePorts { Run = run };
        var old = await new GetJob(ports).ExecuteAsync(Job, ports.Consumer, 1, default);
        var payload = JsonNode.Parse(JsonSerializer.Serialize(old, ContractJsonContext.Default.JobResponse))!.AsObject();
        payload["eventId"] = Event.ToString(); payload["eventType"] = name == "webhook-failed" ? "job.failed" : "job.completed";
        payload["occurredAtUtc"] = f.Clock.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        string? raw = run.ResultJson;
        if (name == "webhook-raw-result")
        {
            // Historical/raw branch is part of the real delivery pipeline; keeps non-normalized decimals.
            payload.Remove("requestedDocumentType"); payload.Remove("documentType"); payload.Remove("resultType");
            raw = Result(false, false).Replace("0.85", "0.850", StringComparison.Ordinal);
        }
        f.Delivery = f.Delivery with { EventId = Event, JobId = Job, PayloadJson = payload.ToJsonString() };
        f.Context = new(Correlation, raw);
        JsonObject? captured = null;
        using var capture = new Capture(async message => {
            var headers = new JsonObject();
            foreach (var h in message.Headers.Concat(message.Content!.Headers).OrderBy(h => h.Key, StringComparer.Ordinal))
                headers[h.Key] = string.Join(", ", h.Value);
            captured = new JsonObject { ["status"] = 200, ["headers"] = headers,
                ["bodyText"] = await message.Content.ReadAsStringAsync(), ["fakeSecretBase64"] = Convert.ToBase64String(f.Key) };
            return new(HttpStatusCode.OK);
        });
        using var transport = new WebhookHttpTransport(new Factory(capture));
        await f.Processor(transport: transport).ExecuteAsync(f.Delivery);
        Assert.That(captured, Is.Not.Null);
        Assert.That(f.Attempts.Single().Status, Is.EqualTo(200));
        return captured!;
    }

    private sealed class Capture(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
}
