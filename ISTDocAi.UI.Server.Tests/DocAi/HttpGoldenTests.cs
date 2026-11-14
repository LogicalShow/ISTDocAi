using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class HttpGoldenTests
{
    internal static readonly Guid FixedJob = Guid.Parse("11111111-1111-4111-8111-111111111111");
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Stage6", name);
    [TestCase("catalog")]
    [TestCase("intake-bl")]
    [TestCase("intake-manifest")]
    [TestCase("intake-auto")]
    [TestCase("replay")]
    [TestCase("reprocess")]
    [TestCase("empty")]
    [TestCase("malformed")]
    [TestCase("oversized")]
    [TestCase("unsupported")]
    [TestCase("missing-key")]
    [TestCase("invalid-key")]
    [TestCase("multiple-keys")]
    [TestCase("not-ready-before-auth")]
    [TestCase("rate-limit")]
    [TestCase("quota")]
    [TestCase("daily-cap")]
    [TestCase("unknown-api")]
    public async Task V1_golden_semantics_survive_v2_transport(string name)
    {
        using var app = new ApiFactory();
        typeof(FakePorts).GetField("<JobId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app.Ports, FixedJob);
        app.Ports.Models.Add(new("MANIFEST", null, "extract", "manifest-v1", .8m));
        app.Ports.Models.Add(new("AUTO", null, "classify", "doc-router-v1", .8m));
        if (name == "not-ready-before-auth") app.TestDatabaseGate = new DatabaseReadiness();
        if (name == "rate-limit") app.Tokens = 1;
        if (name == "replay") app.Ports.FirstOutcome = IntakeOutcome.Existing;
        if (name == "reprocess") app.Ports.SecondOutcome = IntakeOutcome.Requeued;
        if (name == "daily-cap") app.Ports.PagesToday = 100;
        if (name == "quota")
        {
            const string message = "The page allowance is exhausted or insufficient for this document. Contact IST to renew.";
            app.Ports.IntakeError = new(402, "QUOTA_EXHAUSTED", message, message, requiredPages: 6, availablePages: 4);
        }
        using var client = app.Client(name == "missing-key" ? null : name is "invalid-key" or "not-ready-before-auth" ? "invalid-fake-key" : "test-key-one");
        if (name == "multiple-keys") client.DefaultRequestHeaders.Add("X-Api-Key", "test-key-two");
        if (name == "rate-limit") await client.GetAsync("/api/v2/catalog");
        var post = name.StartsWith("intake-", StringComparison.Ordinal) || new[] { "replay", "reprocess", "empty", "malformed", "oversized", "unsupported", "quota", "daily-cap" }.Contains(name);
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get,
            post ? "/api/v2/jobs" : name == "unknown-api" ? "/api/v2/unknown" : "/api/v2/catalog");
        request.Headers.Add("X-Correlation-Id", "baseline-correlation-1");
        if (post) request.Content = name == "malformed" ? new StringContent("{") : Form(
            name == "empty" ? [] : name == "oversized" ? new byte[21*1024*1024] : name == "unsupported" ? "not a PDF"u8.ToArray() : File.ReadAllBytes(Fixture("baseline.pdf")),
            name == "intake-manifest" ? "MANIFEST" : name == "intake-auto" ? "AUTO" : "BL", name == "reprocess" ? "true" : null);
        using var response = await client.SendAsync(request);
        await AssertGolden(name, response);
        Assert.That(app.Serializer.Calls, Is.Zero);
        Assert.That(app.Logs.Payloads, Is.Empty, "No DocAi request payload may enter template DB logging.");
    }
    [TestCase("job-owner")]
    [TestCase("job-other")]
    [TestCase("job-not-found")]
    [TestCase("job-specific-run")]
    [TestCase("usage")]
    [TestCase("topup-cycles")]
    [TestCase("topup-cycle")]
    [TestCase("cycle-documents-paging")]
    public async Task Read_golden_semantics_survive_v2_transport(string name)
    {
        using var app = new ApiFactory();
        typeof(FakePorts).GetField("<JobId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app.Ports, FixedJob);
        if (name.StartsWith("job-", StringComparison.Ordinal)) app.Ports.Run = new(FixedJob, 2, "baseline-request-1", "Queued", null, null, app.Clock.Now, null, 1, null, null, null);
        if (name == "job-other") app.Ports.RunConsumerId = 2;
        if (name == "job-not-found") app.Ports.Run = null;
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(Fixture(name + ".json")));
        var path = fixture.RootElement.GetProperty("request").GetProperty("path").GetString()!.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal);
        using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Correlation-Id", "baseline-correlation-1");
        using var response = await client.GetAsync(path);
        await AssertGolden(name, response);
        if (name.StartsWith("job-", StringComparison.Ordinal) && response.IsSuccessStatusCode)
            await ReadSurfaceTests.AssertCanonical(app, response);
        ReadSurfaceTests.AssertNoPayloadLogs(app);
    }
    internal static MultipartFormDataContent Form(byte[] bytes, string type = "BL", string? reprocess = null, string requestId = "baseline-request-1")
    {
        var form = new MultipartFormDataContent("baseline-fixed-boundary");
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new("application/pdf");
        form.Add(file, "file", "baseline.pdf"); form.Add(new StringContent(type), "documentType");
        form.Add(new StringContent(requestId), "clientRequestId");
        if (reprocess is not null) form.Add(new StringContent(reprocess), "reprocess");
        return form;
    }
    internal static async Task AssertGolden(string name, HttpResponseMessage response, bool sql = false)
    {
        using var golden = JsonDocument.Parse(File.ReadAllBytes(Fixture(name + ".json")));
        var expected = golden.RootElement.GetProperty("response");
        Assert.That((int)response.StatusCode, Is.EqualTo(expected.GetProperty("status").GetInt32()), name);
        Assert.That(response.Headers.GetValues("X-Correlation-Id").Single(), Is.EqualTo("baseline-correlation-1"));
        var oldText = expected.GetProperty("bodyText").GetString()!;
        if (oldText.Length == 0) { Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty); return; }
        var old = JsonNode.Parse(oldText)!;
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.That(actual["IsSuccess"]!.GetValue<bool>(), Is.EqualTo((int)response.StatusCode < 400));
        if ((int)response.StatusCode < 400)
        {
            var value = actual["Value"]!;
            if (sql && value["JobId"] is not null)
            {
                value["JobId"] = FixedJob.ToString();
                if (value["StatusUrl"] is not null) value["StatusUrl"] = System.Text.RegularExpressions.Regex.Replace(value["StatusUrl"]!.GetValue<string>(), "(?<=/jobs/)[0-9a-f-]{36}", FixedJob.ToString());
            }
            Assert.That(JsonNode.DeepEquals(Normalize(old), Normalize(sql ? NormalizeSqlRead(value) : value)), Is.True, name + " Value differs");
            if (name.Contains("job-", StringComparison.Ordinal) || name.Contains("usage", StringComparison.Ordinal) || name.Contains("topup-", StringComparison.Ordinal) || name.Contains("cycle-documents", StringComparison.Ordinal))
                Assert.That(JsonNode.DeepEquals(NormalizeRead(old), NormalizeRead(sql ? NormalizeSqlRead(value) : value)), Is.True, name + " read member/null presence differs");
        }
        else
        {
            Assert.That(actual["Error"]!["Code"]!.GetValue<string>(), Is.EqualTo(old["code"]!.GetValue<string>()));
            Assert.That(actual["Error"]!["Message"]!.GetValue<string>(), Is.EqualTo(old["detail"]!.GetValue<string>()));
            Assert.That(actual["Extensions"]!["CorrelationId"]!.GetValue<string>(), Is.EqualTo(old["correlationId"]!.GetValue<string>()));
            foreach (var key in new[] { "requiredPages", "availablePages" })
                if (old[key] is not null) Assert.That(actual["Extensions"]![char.ToUpperInvariant(key[0])+key[1..]]!.GetValue<long>(), Is.EqualTo(old[key]!.GetValue<long>()));
        }
        foreach (var h in expected.GetProperty("headers").EnumerateArray())
        {
            var key = h.GetProperty("name").GetString()!;
            if (key is not ("Retry-After" or "Location" or "Cache-Control")) continue;
            Assert.That(response.Headers.Contains(key), Is.True, key);
            var header = response.Headers.GetValues(key).Single();
            if (sql && key == "Retry-After" && response.StatusCode == HttpStatusCode.TooManyRequests)
                Assert.That(int.Parse(header, System.Globalization.CultureInfo.InvariantCulture), Is.InRange(1,86400));
            else if (sql && key == "Location")
                Assert.That(header, Does.EndWith("?runNo="+old["runNo"]!.GetValue<int>()));
            else Assert.That(header, Is.EqualTo(h.GetProperty("values")[0].GetString()!.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal)));
        }
    }
    // Same generated job-id and time normalization as Baseline.Capture.Response.
    private static JsonNode NormalizeSqlRead(JsonNode node)
    {
        var copy = node.DeepClone();
        void Visit(JsonNode? current)
        {
            if (current is JsonArray array) { foreach (var item in array) Visit(item); }
            if (current is not JsonObject obj) return;
            foreach (var pair in obj.ToArray())
            {
                if (pair.Value is JsonValue && pair.Key.Equals("JobId", StringComparison.OrdinalIgnoreCase)) obj[pair.Key] = FixedJob.ToString();
                else if (pair.Value is JsonValue && new[] { "CreatedAtUtc", "CompletedAtUtc", "ChargedAtUtc", "StartAtUtc", "EndAtUtc", "RecordedAtUtc" }.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    obj[pair.Key] = "2026-10-25T12:00:00+00:00";
                else Visit(pair.Value);
            }
        }
        Visit(copy); return copy;
    }
    private static JsonNode? NormalizeRead(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new KeyValuePair<string,JsonNode?>(p.Key.ToLowerInvariant(),NormalizeRead(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(NormalizeRead).ToArray()),
        _ => node?.DeepClone()
    };
    private static JsonNode? Normalize(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.Where(p => p.Value is not null).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new KeyValuePair<string,JsonNode?>(p.Key.ToLowerInvariant(),Normalize(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(Normalize).ToArray()),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(text.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal)),
        _ => node?.DeepClone()
    };
}
