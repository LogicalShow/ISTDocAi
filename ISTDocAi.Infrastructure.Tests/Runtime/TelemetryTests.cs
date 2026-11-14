using ProcessingWorkerFakes = ISTDocAi.Infrastructure.Tests.DocAi.Worker.WorkerFakes;
using Rules = ISTDocAi.Infrastructure.Tests.DocAi.Worker.Rules;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
using ISTDocAi.Application.Services.DocAi.ResumePolicyService;
using ISTDocAi.Application.Services.DocAi.RetryPolicyService;
using ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
using ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Common.Mappings.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using System.Text.RegularExpressions;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ISTDocAi.Infrastructure.Configuration.DocAi;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class TelemetryTests
{
    [Test]
    public async Task ProcessJob_log_has_event_errorCode_and_duration_attributes_in_OpenTelemetry()
    {
        var capture = new Capture();
        using var factory = LoggerFactory.Create(b => b.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.ParseStateValues = true;
            o.IncludeFormattedMessage = true;
            o.AddProcessor(capture);
        }));
        using var upstream = new Activity("unsampled-upstream").SetIdFormat(ActivityIdFormat.W3C).Start();
        upstream.ActivityTraceFlags = ActivityTraceFlags.None;
        var f = new ProcessingWorkerFakes();
        var processor = new ProcessJob(f, f, f, f, f, f, f, f, f, f, f.Clock, factory.CreateLogger<ProcessJob>(), Rules.Get<ITakeoverPolicyService>(), Rules.Get<IResumePolicyService>(), Rules.Get<IRetryPolicyService>(), Rules.Get<IErrorClassifierService>(), Rules.Get<IClassifierPolicyService>());
        await processor.ExecuteAsync(f.Job, default);
        var completed = PortAssert.Single(capture.Attributes, x => Equals(x.GetValueOrDefault("event"), AlertEvents.JobCompleted));
        PortAssert.Contains("errorCode", completed.Keys);
        PortAssert.Contains("durationMs", completed.Keys);
    }
    [Test]
    public async Task Azure_exporter_serializes_ProcessJob_event_as_a_trace_custom_dimension()
    {
        using var handler = new Ingestion();
        using var client = new HttpClient(handler);
        using var exporter = new AzureMonitorLogExporter(new AzureMonitorExporterOptions
        {
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/",
            Transport = new HttpClientTransport(client),
            DisableOfflineStorage = true,
            EnableTraceBasedLogsSampler = false
        });
        using var factory = LoggerFactory.Create(b => b.AddOpenTelemetry(o =>
        {
            o.ParseStateValues = true;
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
        }));
        using var upstream = new Activity("unsampled-upstream").SetIdFormat(ActivityIdFormat.W3C).Start();
        upstream.ActivityTraceFlags = ActivityTraceFlags.None;
        var f = new ProcessingWorkerFakes();
        var processor = new ProcessJob(f, f, f, f, f, f, f, f, f, f, f.Clock, factory.CreateLogger<ProcessJob>(), Rules.Get<ITakeoverPolicyService>(), Rules.Get<IResumePolicyService>(), Rules.Get<IRetryPolicyService>(), Rules.Get<IErrorClassifierService>(), Rules.Get<IClassifierPolicyService>());
        await processor.ExecuteAsync(f.Job, default);
        var failed = new ProcessingWorkerFakes { Submit = new SubmitResult.Rejected(400, "INVALID_DOCUMENT", null, null) };
        await new ProcessJob(failed, failed, failed, failed, failed, failed, failed, failed, failed, failed,
            failed.Clock, factory.CreateLogger<ProcessJob>(), Rules.Get<ITakeoverPolicyService>(), Rules.Get<IResumePolicyService>(), Rules.Get<IRetryPolicyService>(), Rules.Get<IErrorClassifierService>(), Rules.Get<IClassifierPolicyService>()).ExecuteAsync(failed.Job, default);
        factory.CreateLogger("webhook-test").LogWarning("{event} statusCode={statusCode}", "webhook.failed", 503);
        var dimensions = handler.Bodies.SelectMany(body => body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            var properties = dimensions.Select(x => x.RootElement.GetProperty("data").GetProperty("baseData").GetProperty("properties")).ToArray();
            var completed = PortAssert.Single(properties, x => x.TryGetProperty("event", out var e) && e.GetString() == AlertEvents.JobCompleted);
            PortAssert.True(completed.TryGetProperty("durationMs", out _));
            PortAssert.Contains(properties, x => x.TryGetProperty("errorCode", out var code) && code.GetString() == "INVALID_DOCUMENT");
            PortAssert.Contains(properties, x => x.TryGetProperty("statusCode", out var code) && code.GetString() == "503");
            PortAssert.DoesNotContain(handler.Bodies, body => body.Contains("PRIVATE"));
        }
        finally { foreach (var item in dimensions) item.Dispose(); }
    }





    [Test]
    public void Export_is_opt_in_and_distro_disables_both_samplers()
    {
        var services = new ServiceCollection();
        services.AddDocAiTelemetry(new ConfigurationBuilder().Build());
        PortAssert.Empty(services);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/"
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDocAiTelemetry(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
        PortAssert.Equal(1.0F, options.SamplingRatio);
        PortAssert.Null(options.TracesPerSecond);
        PortAssert.False(options.EnableTraceBasedLogsSampler);
        var logging = provider.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;
        PortAssert.True(logging.ParseStateValues);
        PortAssert.True(logging.IncludeScopes);
    }
    
    [TestCase("https://test.vault.azure.net/secrets/PRIVATE?version=PRIVATE", false)]
    [TestCase("https://consumer.example/PRIVATE?key=PRIVATE", false)]
    [TestCase("https://di.cognitiveservices.azure.com/PRIVATE?key=PRIVATE", true)]
    public void Sensitive_dependencies_export_host_only(string url, bool webhook)
    {
        var config = Config();
        using var source = new ActivitySource("test");
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource("test")
            .AddProcessor(new SafeDependencyProcessor(config)).Build();
        using var activity = source.StartActivity("GET /PRIVATE", ActivityKind.Client)!;
        activity.SetTag("url.full", url);
        activity.SetTag("http.url", url);
        activity.SetTag("url.path", "/PRIVATE");
        activity.SetTag("url.query", "PRIVATE");
        activity.SetTag("http.target", "/PRIVATE?PRIVATE");
        if (webhook) activity.SetTag("docai.http.sensitive", true);
        activity.Stop();
        PortAssert.Equal(new Uri(url).GetLeftPart(UriPartial.Authority), activity.GetTagItem("url.full"));
        PortAssert.Equal(new Uri(url).GetLeftPart(UriPartial.Authority), activity.GetTagItem("http.url"));
        PortAssert.DoesNotContain("PRIVATE", activity.DisplayName);
        PortAssert.DoesNotContain(activity.TagObjects, x => x.Value?.ToString()?.Contains("PRIVATE") == true);
    }
    
    [TestCase("https://di.cognitiveservices.azure.com/documentintelligence/path?api-version=2024-11-30")]
    [TestCase("https://store.blob.core.windows.net/documents/id")]
    public void DI_and_blob_keep_default_telemetry(string url)
    {
        using var activity = new Activity("default").Start();
        activity.SetTag("url.full", url);
        new SafeDependencyProcessor(Config()).OnEnd(activity);
        PortAssert.Equal(url, activity.GetTagItem("url.full"));
    }
    [Test]
    public void SQL_command_and_parameters_are_removed_before_export()
    {
        using var activity = new Activity("SQL").Start();
        activity.SetTag("db.system", "mssql");
        activity.SetTag("db.statement", "PRIVATE");
        activity.SetTag("db.query.text", "PRIVATE");
        activity.SetTag("db.query.parameter.value", "PRIVATE");
        new SafeDependencyProcessor(Config()).OnEnd(activity);
        PortAssert.DoesNotContain(activity.TagObjects, x => Equals(x.Value, "PRIVATE"));
    }


    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocAi.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["DocAi:DocumentIntelligence:Endpoint"] = "https://di.cognitiveservices.azure.com/",
        ["DocAi:Blob:AccountUrl"] = "https://store.blob.core.windows.net/"
    }).Build();
    private sealed class Ingestion : HttpMessageHandler
    {
        public ConcurrentQueue<string> Bodies { get; } = new();
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > 2 && bytes[0] == 31 && bytes[1] == 139)
            {
                using var compressed = new MemoryStream(bytes);
                using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var decoded = new MemoryStream();
                await gzip.CopyToAsync(decoded, cancellationToken);
                bytes = decoded.ToArray();
            }
            Bodies.Enqueue(Encoding.UTF8.GetString(bytes));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
    private sealed class Capture : BaseProcessor<LogRecord>
    {
        public ConcurrentQueue<Dictionary<string, object?>> Attributes { get; } = new();
        public override void OnEnd(LogRecord data) => Attributes.Enqueue(data.Attributes!.ToDictionary(x => x.Key, x => x.Value));
    }
}
