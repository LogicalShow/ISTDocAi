using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class TransportSafetyTests
{
    [TestCase(400,"INVALID_REQUEST")] [TestCase(401,"UNAUTHORIZED")]
    [TestCase(402,"QUOTA_EXHAUSTED")] [TestCase(403,"FORBIDDEN")]
    [TestCase(404,"JOB_NOT_FOUND")] [TestCase(409,"INVALID_REQUEST")]
    [TestCase(413,"FILE_TOO_LARGE")] [TestCase(415,"UNSUPPORTED_FILE_TYPE")]
    [TestCase(422,"NOT_IN_CATALOG")] [TestCase(429,"RATE_LIMITED")]
    [TestCase(500,"INTERNAL_ERROR")] [TestCase(503,"DATABASE_NOT_READY")]
    public async Task Mapper_preserves_explicit_HTTP_status_and_error_code(int status,string code)
    {
        using var app = new ApiFactory(); using var scope = app.Services.CreateScope();
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Items["correlationId"] = "mapper-test"; context.Response.Body = new MemoryStream();
        await ISTDocAi.UI.Server.Base.DocAi.DocAiTransport.Failure(context,new(status,code,"Fixed title","Fixed detail",5,6,4),default);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.That(context.Response.StatusCode, Is.EqualTo(status));
        Assert.That(json.RootElement.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo(code));
        Assert.That(context.Response.Headers.RetryAfter.ToString(), Is.EqualTo("5"));
        Assert.That(json.RootElement.GetProperty("Extensions").GetProperty("RequiredPages").GetInt64(), Is.EqualTo(6));
    }
    [TestCase(false)] [TestCase(true)]
    public async Task Request_payload_logging_is_excluded_on_success_and_exception(bool fail)
    {
        using var app = new ApiFactory(); app.Ports.Throw = fail;
        using var response = await app.Client().PostAsync("/api/v2/jobs",IntakeTests.Form(client:"PRIVATE_CLIENT_REQUEST"));
        Assert.That(response.StatusCode, Is.EqualTo(fail ? HttpStatusCode.InternalServerError : HttpStatusCode.Accepted));
        Assert.That(app.Serializer.Calls, Is.Zero); Assert.That(app.Logs.Payloads, Is.Empty);
        Assert.That(app.Serilog.Events, Is.Empty, "Generic request middleware must not capture DocAi exceptions.");
        using var health = await app.Client().GetAsync("/health");
        Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(app.Serilog.Events, Is.Not.Empty, "Control: the capture sink must observe template request logging.");
    }
    [TestCase("en")] [TestCase("ar")] [TestCase("fr")] [TestCase("ar-EG")]
    public async Task Localization_preserves_structured_quota_and_does_not_change_parsing(string culture)
    {
        using var app = new ApiFactory();
        const string english = "The page allowance is exhausted or insufficient for this document. Contact IST to renew.";
        app.Ports.IntakeError = new(402,"QUOTA_EXHAUSTED",english,english,requiredPages:6,availablePages:4);
        using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Culture",culture); client.DefaultRequestHeaders.Add("X-Correlation-Id","localized-quota");
        using var response = await client.PostAsync("/api/v2/jobs",IntakeTests.Form());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PaymentRequired));
        Assert.That(body.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo("QUOTA_EXHAUSTED"));
        var message = body.GetProperty("Error").GetProperty("Message").GetString();
        Assert.That(message, culture == "ar" ? Is.Not.EqualTo(english) : Is.EqualTo(english));
        if (culture == "ar")
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
            var resource = System.Xml.Linq.XDocument.Load(Path.Combine(root!.FullName,
                "ISTDocAi.Application", "Common", "Localization", "Messages.ar.resx"));
            var expected = resource.Root!.Elements("data").Single(d => (string?)d.Attribute("name") == "QUOTA_EXHAUSTED").Element("value")!.Value;
            Assert.That(message, Is.EqualTo(expected));
            Assert.That(message!.Any(c => c is >= '\u0600' and <= '\u06ff' && char.IsLetter(c)), Is.True);
            Assert.That(message, Does.Not.Contain("?"));
        }
        var extension = body.GetProperty("Extensions");
        Assert.That(extension.GetProperty("RequiredPages").GetInt64(), Is.EqualTo(6));
        Assert.That(extension.GetProperty("AvailablePages").GetInt64(), Is.EqualTo(4));
        Assert.That(extension.GetProperty("CorrelationId").GetString(), Is.EqualTo("localized-quota"));
    }
    [TestCase(false)] [TestCase(true)]
    public async Task Gate_precedes_authentication_and_keeps_liveness_available(bool failed)
    {
        var gate = new DatabaseReadiness(); if (failed) gate.MarkFailed("DATABASE_SCHEMA_UNRECOGNIZED");
        using var app = new ApiFactory { TestDatabaseGate = gate };
        using var client = app.Client("invalid-key");
        foreach (var path in new[] { "/api/v2/catalog", "/api/v2/jobs", "/api/v2/unknown" })
        {
            using var response = await client.GetAsync(path);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(body.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo(gate.Status.Code));
            Assert.That(body.GetProperty("Extensions").GetProperty("CorrelationId").GetString(), Is.Not.Empty);
        }
        Assert.That(app.Ports.ConsumerReads, Is.Zero);
        Assert.That((await client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetAsync("/health/ready")).StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        if (!failed) { gate.MarkReady(); Assert.That((await app.Client().GetAsync("/api/v2/catalog")).StatusCode, Is.EqualTo(HttpStatusCode.OK)); }
    }
    [Test]
    public async Task V1_is_absent_even_while_database_is_pending()
    {
        using var app = new ApiFactory { TestDatabaseGate = new DatabaseReadiness() };
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/api/v1/catalog");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(app.Ports.ConsumerReads, Is.Zero);
    }
    [Test]
    public async Task Multi_invalid_input_retains_original_rejection_order()
    {
        using var app = new ApiFactory(); using var client = app.Client();
        using var auto = await client.PostAsync("/api/v2/jobs",IntakeTests.Form("bad"u8.ToArray(),type:"AUTO",line:"LINE"));
        Assert.That((int)auto.StatusCode, Is.EqualTo(422));
        Assert.That(app.Ports.CatalogReads, Is.Zero);
        using var unsupported = await client.PostAsync("/api/v2/jobs",IntakeTests.Form("bad"u8.ToArray(),type:"UNKNOWN"));
        Assert.That((int)unsupported.StatusCode, Is.EqualTo(415));
        Assert.That(app.Ports.CatalogReads, Is.Zero);
    }
    [Test]
    public async Task Consumers_have_separate_buckets_and_unauthorized_calls_do_not_spend_tokens()
    {
        using var app = new ApiFactory { Tokens=1 };
        app.Ports.OtherConsumers.Add(app.Ports.Consumer with { ConsumerId=2, ApiKeyHash1=System.Security.Cryptography.SHA256.HashData("other-key"u8), ApiKeyHash2=null });
        Assert.That((await app.Client("bad-key").GetAsync("/api/v2/catalog")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await app.Client().GetAsync("/api/v2/catalog")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await app.Client("other-key").GetAsync("/api/v2/catalog")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await app.Client().GetAsync("/api/v2/catalog")).StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That((await app.Client().GetAsync("/api/v2/unknown")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
public sealed class SafeExceptionLoggingTests
{
    [Test]
    public async Task Unexpected_failure_logs_type_stack_and_correlation_without_exception_message() => await AssertSafeFailure(false);

    [Test]
    public async Task Catalog_failure_logs_request_correlation_and_stack_without_exception_message() => await AssertSafeFailure(true);

    private static async Task AssertSafeFailure(bool catalog)
    {
        using var app = new ApiFactory(); app.Ports.Throw = !catalog; app.Ports.ThrowCatalog = catalog;
        var logs = new CaptureProvider();
        using var factory = app.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Api-Key","test-key-one");
        client.DefaultRequestHeaders.Add("X-Correlation-Id","safe-log-test");
        using var response = catalog ? await client.GetAsync("/api/v2/catalog")
            : await client.PostAsync("/api/v2/jobs",IntakeTests.Form(client:"PRIVATE_CLIENT_REQUEST"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var entry = logs.Entries.Single(e => e.Text.Contains("INTERNAL_ERROR",StringComparison.Ordinal));
        Assert.That(entry.Error, Is.Null);
        Assert.That(entry.Text, Does.Contain(typeof(InvalidOperationException).FullName));
        Assert.That(entry.Scopes["CorrelationId"], Is.EqualTo("safe-log-test"));
        Assert.That(entry.Properties["ExceptionType"], Is.EqualTo(typeof(InvalidOperationException).FullName));
        Assert.That(entry.Properties["StackTrace"] as string, Does.Contain(catalog ? nameof(FakePorts.GetCatalogAsync) : nameof(FakePorts.IntakeAsync)));
        foreach (var text in new[] { "internal exception text", "test-key-one", "PRIVATE_CLIENT_REQUEST", "display.pdf" })
            Assert.That(string.Join(";",logs.Entries.Select(e=>e.Text)), Does.Not.Contain(text));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Not.Contain("internal exception text").And.Not.Contain(typeof(InvalidOperationException).FullName));
        using var json = JsonDocument.Parse(body);
        Assert.That(json.RootElement.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo("INTERNAL_ERROR"));
        Assert.That(json.RootElement.GetProperty("Extensions").GetProperty("CorrelationId").GetString(), Is.EqualTo("safe-log-test"));
        Assert.That(app.Serializer.Calls, Is.Zero); Assert.That(app.Logs.Payloads, Is.Empty);
        Assert.That(app.Serilog.Events, Is.Empty, "Generic request middleware must not capture DocAi exceptions.");
    }
    private sealed record Entry(string Text, Exception? Error, IReadOnlyDictionary<string, object?> Properties, IReadOnlyDictionary<string, object?> Scopes);
    private sealed class CaptureProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
        public List<Entry> Entries { get; } = [];
        public ILogger CreateLogger(string category) => new CaptureLogger(this);
        public void Dispose() { }
        private sealed class CaptureLogger(CaptureProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState,Exception?,string> formatter)
            {
                var capturedScopes = new Dictionary<string, object?>();
                owner.scopes.ForEachScope((scope, fields) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> values)
                        foreach (var value in values) fields[value.Key] = value.Value;
                }, capturedScopes);
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.ToDictionary(p => p.Key, p => p.Value) : new Dictionary<string, object?>();
                owner.Entries.Add(new(formatter(state,error),error,properties,capturedScopes));
            }
        }
    }
}
