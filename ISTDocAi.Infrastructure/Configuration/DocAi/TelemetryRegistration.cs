using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Trace;

namespace ISTDocAi.Infrastructure.Configuration.DocAi;

public static class TelemetryRegistration
{
    public static IServiceCollection AddDocAiTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connection)) return services;
        services.AddOpenTelemetry().WithTracing(t => t.AddProcessor(new SafeDependencyProcessor(configuration))).UseAzureMonitor(o =>
        {
            o.ConnectionString = connection;
            o.SamplingRatio = 1.0F;
            o.TracesPerSecond = null;
            o.EnableTraceBasedLogsSampler = false;
        });
        if (configuration.GetValue<bool>("DocAi:Telemetry:UseEntra"))
            services.AddOptions<AzureMonitorOptions>().Configure<Azure.Core.TokenCredential>((o, credential) => o.Credential = credential);
        services.Configure<HttpClientTraceInstrumentationOptions>(o =>
        {
            o.RecordException = false;
            o.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                if (request.Options.TryGetValue(new HttpRequestOptionsKey<bool>("DocAi.SensitiveUrl"), out var sensitive) && sensitive)
                    activity.SetTag("docai.http.sensitive", true);
            };
        });
        services.Configure<OpenTelemetryLoggerOptions>(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.ParseStateValues = true;
        });
        return services;
    }
}

// Runs before exporters. Unknown HTTP destinations (including registered consumer callbacks)
// and Azure SDK Key Vault spans expose only a host. DI and Blob retain default telemetry.
public sealed class SafeDependencyProcessor(IConfiguration configuration) : BaseProcessor<Activity>
{
    private readonly HashSet<string> defaultHosts = new[]
    {
        configuration["DocAi:DocumentIntelligence:Endpoint"], configuration["DocAi:Blob:AccountUrl"]
    }.Where(x => Uri.TryCreate(x, UriKind.Absolute, out _))
     .Select(x => new Uri(x!).Host).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public override void OnEnd(Activity activity)
    {
        var sql = activity.Source.Name.Contains("SqlClient", StringComparison.OrdinalIgnoreCase)
            || activity.GetTagItem("db.system") is not null || activity.GetTagItem("db.system.name") is not null;
        if (sql)
        {
            foreach (var tag in activity.TagObjects.ToArray())
                if (new[] { "db.statement", "db.query", "db.command", "db.sql", "db.parameters" }
                    .Any(prefix => tag.Key.StartsWith(prefix, StringComparison.Ordinal)))
                    activity.SetTag(tag.Key, null);
            activity.DisplayName = "SQL";
            activity.SetStatus(activity.Status);
        }
        if (activity.Kind is not (ActivityKind.Client or ActivityKind.Internal)) return;
        var urls = activity.TagObjects.Where(t => t.Key is "url.full" or "http.url").ToArray();
        foreach (var tag in urls)
        {
            if (!Uri.TryCreate(tag.Value?.ToString(), UriKind.Absolute, out var uri) || (defaultHosts.Contains(uri.Host) && activity.GetTagItem("docai.http.sensitive") is not true)) continue;
            activity.SetTag(tag.Key, new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port).Uri.GetLeftPart(UriPartial.Authority));
            activity.DisplayName = activity.GetTagItem("http.request.method")?.ToString()
                ?? activity.GetTagItem("http.method")?.ToString() ?? "HTTP";
            activity.SetStatus(activity.Status);
            foreach (var item in activity.TagObjects.ToArray())
                if (item.Key is "url.path" or "url.query" or "http.target" or "http.route"
                    || item.Key.StartsWith("exception.", StringComparison.Ordinal)) activity.SetTag(item.Key, null);
        }
    }
}
