using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class ReadSurfaceTests
{
    internal static void AssertNoPayloadLogs(ApiFactory app)
    {
        Assert.That(app.Serializer.Calls, Is.Zero);
        Assert.That(app.Logs.Payloads, Is.Empty);
        Assert.That(app.Serilog.Events, Is.Empty);
    }
    internal static async Task AssertCanonical(ApiFactory app, HttpResponseMessage response)
    {
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISTDocAi.Application.Services.DocAi.Intake.GetJob>();
        var b = await service.ExecuteAsync(app.Ports.Run!.JobId, app.Ports.Consumer, app.Ports.Run.RunNo, default);
        using var old = JsonDocument.Parse(JsonSerializer.Serialize(b, ContractJsonContext.Default.JobResponse));
        var actual = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.That(Encoding.UTF8.GetBytes(actual.GetProperty("Value").GetProperty("Result").GetRawText()),
            Is.EqualTo(Encoding.UTF8.GetBytes(old.RootElement.GetProperty("result").GetRawText())));
    }
    [TestCase("en", "ANALYSIS_FAILED")] [TestCase("ar", "ANALYSIS_FAILED")]
    [TestCase("fr", "ANALYSIS_FAILED")] [TestCase("ar", "UNKNOWN_SAVED_CODE")]
    public async Task Failed_job_is_successful_read_with_localized_or_verbatim_saved_error(string culture, string code)
    {
        using var app = new ApiFactory();
        app.Ports.Run = new(app.Ports.JobId, 1, "request", "Failed", code, "Saved display message", app.Clock.Now, app.Clock.Now, 1, null, null, null);
        using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Culture", culture);
        using var response = await client.GetAsync($"/api/v2/jobs/{app.Ports.JobId}");
        var root = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(root.GetProperty("IsSuccess").GetBoolean(), Is.True);
        var error = root.GetProperty("Value").GetProperty("Error");
        Assert.That(error.GetProperty("Code").GetString(), Is.EqualTo(code));
        var expected = culture == "ar" && code == "ANALYSIS_FAILED" ? ArabicResource(code) : "Saved display message";
        Assert.That(error.GetProperty("Message").GetString(), Is.EqualTo(expected));
        if (culture == "ar" && code == "ANALYSIS_FAILED") AssertArabic(expected);
        Assert.That(response.Headers.Contains("Retry-After"), Is.False);
        Assert.That(response.Headers.CacheControl, Is.Null);
        await AssertCanonical(app, response); AssertNoPayloadLogs(app);
    }
    internal static string ArabicResource(string code)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"ISTDocAi.sln"))) root = root.Parent;
        return XDocument.Load(Path.Combine(root!.FullName,"ISTDocAi.Application/Common/Localization/Messages.ar.resx"))
            .Root!.Elements("data").Single(e => (string?)e.Attribute("name") == code).Element("value")!.Value;
    }
    private static void AssertArabic(string value)
    {
        Assert.That(value.Any(c => c is >= '\u0600' and <= '\u06ff' && char.IsLetter(c)), Is.True);
        Assert.That(value, Does.Not.Contain("?"));
    }
    [TestCase("en")] [TestCase("ar")]
    public async Task Missing_topup_returns_localized_envelope(string culture)
    {
        using var app = new ApiFactory(); using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Culture", culture);
        using var response = await client.GetAsync("/api/v2/usage/topups/999");
        var root = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(root.GetProperty("IsFailure").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo("TOPUP_NOT_FOUND"));
        var message = root.GetProperty("Error").GetProperty("Message").GetString();
        Assert.That(message, Is.EqualTo(culture == "ar" ? ArabicResource("TOPUP_NOT_FOUND") : "No such top-up cycle or cursor exists for this consumer."));
        if (culture == "ar") AssertArabic(message!);
        Assert.That(root.GetProperty("Extensions").GetProperty("CorrelationId").GetString(), Is.EqualTo(response.Headers.GetValues("X-Correlation-Id").Single()));
        Assert.That(response.Headers.CacheControl!.NoStore, Is.True); AssertNoPayloadLogs(app);
    }
    [TestCase("/api/v2/jobs/not-a-guid")] [TestCase("/api/v2/jobs/11111111-1111-4111-8111-111111111111?runNo=1&runNo=2")]
    public async Task Invalid_job_input_never_reads(string path)
    {
        using var app = new ApiFactory(); using var response = await app.Client().GetAsync(path);
        await IntakeTests.Error(response, 400, "INVALID_REQUEST"); Assert.That(app.Ports.RunReads, Is.Zero);
    }
    [TestCase(false)] [TestCase(true)]
    public async Task Topup_no_store_precedes_readiness_and_auth(bool pending)
    {
        using var app = new ApiFactory(); if (pending) app.TestDatabaseGate = new DatabaseReadiness();
        using var response = await app.Client(null).GetAsync("/api/v2/usage/topups/1/documents");
        Assert.That(response.StatusCode, Is.EqualTo(pending ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.CacheControl!.NoStore, Is.True); Assert.That(app.Ports.UsageConsumerId, Is.Null);
    }
    [TestCase("/api/v2/usage")] [TestCase("/api/v2/usage/topups")]
    [TestCase("/api/v2/usage/topups/1")] [TestCase("/api/v2/usage/topups/1/documents")]
    public async Task Read_queries_never_enter_payload_logging(string path)
    {
        using var app = new ApiFactory(); using var response = await app.Client().GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)); AssertNoPayloadLogs(app);
    }
    [Test]
    public async Task BL_result_bytes_are_contract_serialized()
    {
        using var app = new ApiFactory();
        var result = new CanonicalResult { ContractVersion="1.0", DocumentTypeCode="BL", ShippingLineCode=null, ModelId="bl", PageCount=1, Fields=new() { ["name"] = new() { Value="قيمة", Page=1, Confidence=.99 } }, Containers=[], Flags=[] };
        app.Ports.Run = new(app.Ports.JobId, 1, "request", "Completed", null, null, app.Clock.Now, app.Clock.Now, 1, "1.0", CanonicalResultCodec.Serialize(result), 0);
        using var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}");
        await AssertCanonical(app, response); AssertNoPayloadLogs(app);
    }
    [TestCase("/api/v2/usage/topups/unknown/route", true)]
    [TestCase("/api/v2/usage/topups-extra", false)]
    public async Task Unknown_routes_stay_empty_and_use_source_segment_cache_rule(string path, bool noStore)
    {
        using var app = new ApiFactory(); using var response = await app.Client().GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(response.Headers.CacheControl?.NoStore == true, Is.EqualTo(noStore));
    }
    [Test]
    public async Task Unauthenticated_usage_does_not_set_no_store()
    {
        using var app = new ApiFactory(); using var response = await app.Client(null).GetAsync("/api/v2/usage?months=0");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.CacheControl, Is.Null);
    }
}
