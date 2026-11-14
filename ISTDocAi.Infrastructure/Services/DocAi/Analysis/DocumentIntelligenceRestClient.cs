using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Analysis;

public sealed class DocumentIntelligenceRestClient : IDocumentAnalyzer
{
    public const string ApiVersion = "2024-11-30";
    private readonly HttpClient http;
    private readonly AnalysisRateLimits limits;
    private readonly TimeProvider clock;
    private readonly Uri endpoint;
    private readonly BearerTokenCache tokens;
    public DocumentIntelligenceRestClient(HttpClient http, IOptions<DocumentIntelligenceOptions> options, AnalysisRateLimits limits, TimeProvider clock, BearerTokenCache tokens)
    {
        endpoint = new Uri(options.Value.Endpoint.TrimEnd('/') + "/");
        if (endpoint.Scheme != "https" || !endpoint.Host.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase)
            || endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0 || !endpoint.IsDefaultPort)
            throw new ArgumentException("Document Intelligence requires an HTTPS custom subdomain endpoint.");
        this.http = http; this.limits = limits; this.clock = clock; this.tokens = tokens;
    }
    public async Task<bool> CheckAccessAsync(CancellationToken ct)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            await limits.WaitAsync(false, budget.Token);
            var token = await tokens.GetAsync(budget.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(endpoint, $"documentintelligence/info?api-version={ApiVersion}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (AnalysisUnavailableException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    private Uri OperationUri(string location)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || uri.Scheme != endpoint.Scheme || uri.Authority != endpoint.Authority
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.StartsWith("/documentintelligence/", StringComparison.Ordinal))
            throw new ArgumentException("Operation location must belong to the configured Document Intelligence endpoint.");
        return uri;
    }
    private sealed class Permit(DocumentIntelligenceRestClient owner, string token) : SubmitPermit
    {
        private string? reservedToken = token;
        public string Consume(DocumentIntelligenceRestClient caller)
        {
            if (!ReferenceEquals(owner, caller)) throw new ArgumentException("Submit permit belongs to another analyzer.");
            return Interlocked.Exchange(ref reservedToken, null) ?? throw new InvalidOperationException("Submit permit was already consumed.");
        }
    }
    public async Task<SubmitPermit> ReserveSubmitAsync(CancellationToken ct)
    {
        await limits.WaitAsync(true, ct);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(100));
        try { return new Permit(this, await tokens.GetAsync(budget.Token)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable); }
    }
    public async Task<SubmitResult> SubmitAsync(SubmitPermit permit, OperationKind kind, string modelId, ReadOnlyMemory<byte> document, CancellationToken ct)
    {
        if (kind is not (OperationKind.Extract or OperationKind.Classify)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        var token = permit is Permit prepared ? prepared.Consume(this) : throw new ArgumentException("Invalid submit permit.", nameof(permit));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(100));
        var collection = kind == OperationKind.Classify ? "documentClassifiers" : "documentModels";
        var url = new Uri(endpoint, $"documentintelligence/{collection}/{Uri.EscapeDataString(modelId)}:analyze?api-version={ApiVersion}" + (kind == OperationKind.Classify ? "&split=none" : ""));
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { base64Source = Convert.ToBase64String(document.Span) }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, budget.Token);
            var retry = RetryAfter(response);
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                if (!response.Headers.TryGetValues("Operation-Location", out var values)) return new SubmitResult.NoAnswer();
                var location = values.FirstOrDefault();
                try { return new SubmitResult.Accepted(OperationUri(location ?? "").AbsoluteUri, retry); }
                catch (ArgumentException) { return new SubmitResult.NoAnswer(); }
            }
            var (code, message) = await ReadErrorAsync(response, budget.Token);
            return new SubmitResult.Rejected((int)response.StatusCode, code, message, retry);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new SubmitResult.NoAnswer(); }
        catch (HttpRequestException) { return new SubmitResult.NoAnswer(); }
    }
    public async Task<PollResult> PollAsync(string operationLocation, CancellationToken ct)
    {
        var uri = OperationUri(operationLocation);
        await limits.WaitAsync(false, ct);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        string token;
        try { token = await tokens.GetAsync(budget.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable); }
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, budget.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return new PollResult.NotFound();
            if (!response.IsSuccessStatusCode) return new PollResult.HttpError((int)response.StatusCode, RetryAfter(response));
            var json = await response.Content.ReadAsStringAsync(budget.Token);
            using var body = JsonDocument.Parse(json);
            var root = body.RootElement;
            var status = root.GetProperty("status").GetString();
            if (status is "running" or "notStarted") return new PollResult.Running(RetryAfter(response) ?? TimeSpan.FromSeconds(2));
            if (status is "failed" or "canceled" or "skipped")
            {
                var (code, message) = Error(root);
                return new PollResult.Failed(code ?? status, message);
            }
            if (status == "succeeded")
            {
                var result = root.GetProperty("analyzeResult");
                var completed = root.GetProperty("lastUpdatedDateTime").GetDateTimeOffset().ToUniversalTime();
                return new PollResult.Succeeded(result.GetRawText(), completed, result.TryGetProperty("pages", out var pages) ? pages.GetArrayLength() : 0);
            }
            return new PollResult.NoAnswer();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new PollResult.NoAnswer(); }
        catch (HttpRequestException) { return new PollResult.NoAnswer(); }
        catch (JsonException) { return new PollResult.NoAnswer(); }
        catch (InvalidOperationException) { return new PollResult.NoAnswer(); }
        catch (KeyNotFoundException) { return new PollResult.NoAnswer(); }
        catch (FormatException) { return new PollResult.NoAnswer(); }
    }
    public async Task DeleteResultAsync(string operationLocation, CancellationToken ct)
    {
        var uri = OperationUri(operationLocation);
        await limits.WaitAsync(false, ct);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(budget.Token));
        using var response = await http.SendAsync(request, budget.Token);
        response.EnsureSuccessStatusCode();
    }
    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (header?.Date is { } date)
        {
            var now = clock.GetUtcNow();
            return date > now ? date - now : TimeSpan.Zero;
        }
        return null;
    }
    private static (string? Code, string? Message) Error(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return (null, null);
        return (error.TryGetProperty("code", out var code) ? code.GetString() : null,
            error.TryGetProperty("message", out var message) ? message.GetString() : null);
    }
    private static async Task<(string? Code, string? Message)> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return Error(json.RootElement); }
        catch (JsonException) { return (null, null); }
        catch (InvalidOperationException) { return (null, null); }
    }
}
