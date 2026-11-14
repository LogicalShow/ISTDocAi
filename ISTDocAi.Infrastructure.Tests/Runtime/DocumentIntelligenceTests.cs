using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
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
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Azure.Core;

using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class DocumentIntelligenceTests
{
    private const string Endpoint = "https://unit.cognitiveservices.azure.com";
    private const string Operation = Endpoint + "/documentintelligence/documentModels/model/analyzeResults/operation?api-version=2024-11-30";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        private readonly TestClock timers = new();
        public void Advance(TimeSpan duration) { Now += duration; timers.Advance(duration); }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            timers.CreateTimer(callback, state, dueTime, period);
    }
    private sealed class Credential(Clock clock) : TokenCredential
    {
        public int Calls;
        public bool Fail;
        public Func<CancellationToken, Task>? BeforeReturn;
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => throw new NotSupportedException();
        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct)
        {
            PortAssert.Equal(new[] { "https://cognitiveservices.azure.com/.default" }, context.Scopes);
            Interlocked.Increment(ref Calls);
            await Task.Yield();
            if (BeforeReturn is not null) await BeforeReturn(ct);
            if (Fail) throw new Azure.Identity.AuthenticationFailedException("Credential unavailable in test.");
            return new AccessToken(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), clock.Now.AddHours(1));
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private sealed class Harness : IDisposable
    {
        public Clock Clock { get; } = new();
        public Credential Credential { get; }
        public DocumentIntelligenceRestClient Client { get; }
        private readonly BearerTokenCache cache;
        private readonly AnalysisRateLimits limits;
        private readonly HttpClient http;
        public Harness(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, DocumentIntelligenceOptions? settings = null)
        {
            Credential = new(Clock);
            cache = new(Credential, Clock);
            limits = new(Options.Create(settings ?? new DocumentIntelligenceOptions()));
            http = new(new BearerTokenHandler(cache) { InnerHandler = new Handler(async (request, ct) =>
            {
                // Assertions never include the credential value, even on failure.
                PortAssert.True(request.Headers.Authorization?.Scheme == "Bearer");
                PortAssert.True(!string.IsNullOrEmpty(request.Headers.Authorization?.Parameter));
                return await send(request, ct);
            }) }) { Timeout = Timeout.InfiniteTimeSpan };
            Client = new(http, Options.Create(new DocumentIntelligenceOptions { Endpoint = Endpoint }), limits, Clock, cache);
        }
        [TearDown]
    public void Dispose() { http.Dispose(); limits.Dispose(); cache.Dispose(); }
    }
    private static HttpResponseMessage Response(int status, string body = "", string? retry = null)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
        if (retry is not null) response.Headers.TryAddWithoutValidation("Retry-After", retry);
        return response;
    }
    
    [TestCase(200, true)] [TestCase(401, false)] [TestCase(403, false)]
    [TestCase(500, false)] [TestCase(503, false)]
    public async Task Resource_details_checks_exact_URL_without_any_model(int status, bool expected)
    {
        using var h = new Harness((request, _) =>
        {
            PortAssert.Equal(HttpMethod.Get, request.Method);
            PortAssert.Equal(Endpoint + "/documentintelligence/info?api-version=2024-11-30", request.RequestUri!.AbsoluteUri);
            PortAssert.Null(request.Content);
            return Task.FromResult(Response(status));
        });
        PortAssert.Equal(expected, await h.Client.CheckAccessAsync(default));
        PortAssert.Equal(1, h.Credential.Calls);
    }
    [Test]
    public async Task Resource_details_times_out_at_ten_seconds_using_fake_time()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var h = new Harness(async (_, ct) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Response(200);
        });
        var check = h.Client.CheckAccessAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Clock.Advance(TimeSpan.FromSeconds(9));
        PortAssert.False(check.IsCompleted);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        PortAssert.False(await check.WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Test]
    public async Task Resource_details_uses_GET_bucket_and_local_unavailability_is_not_ready()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(200, "{\"status\":\"running\"}")); },
            new() { PollPerSecond = 1, TokenWaitTimeout = TimeSpan.FromMilliseconds(20) });
        await h.Client.PollAsync(Operation, default);
        PortAssert.False(await h.Client.CheckAccessAsync(default));
        PortAssert.Equal(1, calls);
        PortAssert.NotNull(await h.Client.ReserveSubmitAsync(default));
    }
    [Test]
    public async Task Resource_details_credential_failure_is_not_ready_without_HTTP()
    {
        using var h = new Harness((_, _) => throw new InvalidOperationException("Transport must not run."));
        h.Credential.Fail = true;
        PortAssert.False(await h.Client.CheckAccessAsync(default));
    }
    [Test]
    public async Task Resource_details_caller_cancellation_propagates()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200)));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await PortAssert.ThrowsAnyAsync<OperationCanceledException>(() => h.Client.CheckAccessAsync(canceled.Token));
    }
    [Test]
    public async Task Delete_uses_validated_operation_url_bearer_token_and_GET_bucket()
    {
        var sends = 0;
        using var h = new Harness((request, ct) =>
        {
            PortAssert.Equal(HttpMethod.Delete, request.Method);
            PortAssert.Equal(Operation, request.RequestUri!.AbsoluteUri);
            Interlocked.Increment(ref sends);
            return Task.FromResult(Response(204));
        }, new DocumentIntelligenceOptions { AnalyzePerSecond = 1, PollPerSecond = 1, QueueLimit = 1,
            TokenWaitTimeout = TimeSpan.FromMilliseconds(20) });
        await h.Client.DeleteResultAsync(Operation, default);
        await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.DeleteResultAsync(Operation, default));
        PortAssert.Equal(1, sends);
        // DELETE used GET admission; the independent POST bucket remains available.
        PortAssert.NotNull(await h.Client.ReserveSubmitAsync(default));
        await PortAssert.ThrowsAsync<ArgumentException>(() => h.Client.DeleteResultAsync("https://evil.example/documentintelligence/operation", default));
        PortAssert.Equal(1, sends);
    }
    [Test]
    public async Task Delete_non_success_is_reported_to_best_effort_caller()
    {
        using var h = new Harness((request, ct) => Task.FromResult(Response(503)));
        await PortAssert.ThrowsAsync<HttpRequestException>(() => h.Client.DeleteResultAsync(Operation, default));
    }
    
    [TestCase(OperationKind.Extract, "documentModels", "")]
    [TestCase(OperationKind.Classify, "documentClassifiers", "&split=none")]
    public async Task Exact_submit_wire_contract(OperationKind kind, string collection, string suffix)
    {
        using var h = new Harness(async (request, ct) =>
        {
            PortAssert.Equal(HttpMethod.Post, request.Method);
            PortAssert.Equal($"{Endpoint}/documentintelligence/{collection}/model%20id:analyze?api-version=2024-11-30{suffix}", request.RequestUri!.AbsoluteUri);
            PortAssert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            PortAssert.Single(json.RootElement.EnumerateObject());
            PortAssert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), json.RootElement.GetProperty("base64Source").GetString());
            var response = Response(202, retry: "7");
            response.Headers.Add("Operation-Location", Operation);
            return response;
        });
        var accepted = PortAssert.IsType<SubmitResult.Accepted>(await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), kind, "model id", new byte[] { 1, 2, 3 }, default));
        PortAssert.Equal(Operation, accepted.OperationLocation);
        PortAssert.Equal(TimeSpan.FromSeconds(7), accepted.RetryAfter);
    }
    
    [TestCase(400)] [TestCase(401)] [TestCase(403)] [TestCase(404)] [TestCase(429)] [TestCase(500)] [TestCase(503)]
    public async Task Submit_rejections_preserve_status_and_azure_error(int status)
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(status, """{"error":{"code":"ServiceCode","message":"Service message","innererror":{"code":"Detail"}}}""", "19")));
        var rejected = PortAssert.IsType<SubmitResult.Rejected>(await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), OperationKind.Extract, "model", new byte[] { 1 }, default));
        PortAssert.Equal(status, rejected.Status); PortAssert.Equal("ServiceCode", rejected.Code);
        PortAssert.Equal("Service message", rejected.Message); PortAssert.Equal(TimeSpan.FromSeconds(19), rejected.RetryAfter);
    }
    
    [TestCase(null)] [TestCase("https://other.example/documentintelligence/result")]
    public async Task Accepted_without_safe_location_is_no_answer(string? location)
    {
        using var h = new Harness((_, _) =>
        {
            var response = Response(202);
            if (location is not null) response.Headers.Add("Operation-Location", location);
            return Task.FromResult(response);
        });
        PortAssert.IsType<SubmitResult.NoAnswer>(await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), OperationKind.Extract, "model", new byte[] { 1 }, default));
    }
    
    [TestCase("notStarted")] [TestCase("running")]
    public async Task Running_poll_defaults_to_two_seconds(string status)
    {
        using var h = new Harness((request, _) =>
        {
            PortAssert.Equal(HttpMethod.Get, request.Method); PortAssert.Equal(Operation, request.RequestUri!.AbsoluteUri); PortAssert.Null(request.Content);
            return Task.FromResult(Response(200, JsonSerializer.Serialize(new { status })));
        });
        PortAssert.Equal(TimeSpan.FromSeconds(2), PortAssert.IsType<PollResult.Running>(await h.Client.PollAsync(Operation, default)).RetryAfter);
    }
    
    [TestCase("failed")] [TestCase("canceled")] [TestCase("skipped")]
    public async Task Terminal_failures(string status)
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200, JsonSerializer.Serialize(new { status, error = new { code = "FailureCode", message = "Failure message" } }))));
        var failed = PortAssert.IsType<PollResult.Failed>(await h.Client.PollAsync(Operation, default));
        PortAssert.Equal("FailureCode", failed.Code); PortAssert.Equal("Failure message", failed.Message);
    }
    
    [TestCase("canceled")] [TestCase("skipped")]
    public async Task Terminal_without_error_uses_status(string status)
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200, JsonSerializer.Serialize(new { status }))));
        PortAssert.Equal(status, PortAssert.IsType<PollResult.Failed>(await h.Client.PollAsync(Operation, default)).Code);
    }
    [Test]
    public async Task Succeeded_returns_raw_result_pages_and_utc_completion()
    {
        const string raw = """{"pages":[{"pageNumber":1},{"pageNumber":2}],"documents":[]}""";
        using var h = new Harness((_, _) => Task.FromResult(Response(200, """{"status":"succeeded","lastUpdatedDateTime":"2026-10-25T14:00:00+02:00","analyzeResult":RAW}""".Replace("RAW", raw))));
        var result = PortAssert.IsType<PollResult.Succeeded>(await h.Client.PollAsync(Operation, default));
        PortAssert.Equal(raw, result.RawJson); PortAssert.Equal(2, result.Pages);
        PortAssert.Equal(h.Clock.Now, result.CompletedAtUtc); PortAssert.Equal(TimeSpan.Zero, result.CompletedAtUtc.Offset);
    }
    
    [TestCase(400)] [TestCase(401)] [TestCase(403)] [TestCase(429)] [TestCase(500)] [TestCase(503)]
    public async Task Poll_http_errors(int status)
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(status, retry: "301")));
        var result = PortAssert.IsType<PollResult.HttpError>(await h.Client.PollAsync(Operation, default));
        PortAssert.Equal(status, result.Status); PortAssert.Equal(TimeSpan.FromSeconds(301), result.RetryAfter);
    }
    [Test]
    public async Task Poll_not_found_is_distinct()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(404)));
        PortAssert.IsType<PollResult.NotFound>(await h.Client.PollAsync(Operation, default));
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Http_date_retry_after_is_preserved(bool post)
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(429, retry: "Sun, 25 Oct 2026 12:10:00 GMT")));
        var retry = post ? PortAssert.IsType<SubmitResult.Rejected>(await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), OperationKind.Extract, "m", new byte[] { 1 }, default)).RetryAfter
            : PortAssert.IsType<PollResult.HttpError>(await h.Client.PollAsync(Operation, default)).RetryAfter;
        PortAssert.Equal(TimeSpan.FromMinutes(10), retry);
    }
    [Test]
    public async Task Running_retry_after_is_not_capped()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200, """{"status":"running"}""", "900")));
        PortAssert.Equal(TimeSpan.FromMinutes(15), PortAssert.IsType<PollResult.Running>(await h.Client.PollAsync(Operation, default)).RetryAfter);
    }
    
    [TestCase(true, true)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(false, false)]
    public async Task Timeout_or_network_has_no_answer(bool post, bool timeout)
    {
        using var h = new Harness((_, _) => throw (timeout ? new TaskCanceledException() : new HttpRequestException()));
        if (post) PortAssert.IsType<SubmitResult.NoAnswer>(await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), OperationKind.Extract, "m", new byte[] { 1 }, default));
        else PortAssert.IsType<PollResult.NoAnswer>(await h.Client.PollAsync(Operation, default));
    }
    [Test]
    public async Task Caller_cancellation_propagates()
    {
        using var h = new Harness((_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(Response(200)); });
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await PortAssert.ThrowsAnyAsync<OperationCanceledException>(async () => await h.Client.SubmitAsync(await h.Client.ReserveSubmitAsync(default), OperationKind.Extract, "m", new byte[] { 1 }, cts.Token));
        await PortAssert.ThrowsAnyAsync<OperationCanceledException>(() => h.Client.PollAsync(Operation, cts.Token));
    }
    [Test]
    public async Task One_token_serves_concurrent_calls_and_refreshes_at_five_minutes()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200, """{"status":"running"}""")));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => h.Client.PollAsync(Operation, default)));
        PortAssert.Equal(1, h.Credential.Calls);
        h.Clock.Now = h.Clock.Now.AddMinutes(54).AddSeconds(59);
        await h.Client.PollAsync(Operation, default); PortAssert.Equal(1, h.Credential.Calls);
        h.Clock.Now = h.Clock.Now.AddSeconds(1);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => h.Client.PollAsync(Operation, default)));
        PortAssert.Equal(2, h.Credential.Calls);
    }
    [Test]
    public async Task Unsafe_poll_never_reaches_transport()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(200)); });
        await PortAssert.ThrowsAsync<ArgumentException>(() => h.Client.PollAsync("https://other.example/documentintelligence/result", default));
        PortAssert.Equal(0, calls); PortAssert.Equal(0, h.Credential.Calls);
    }
    [Test]
    public async Task Eleventh_post_waits_for_replenishment_and_get_bucket_is_independent()
    {
        using var limits = new AnalysisRateLimits(Options.Create(new DocumentIntelligenceOptions()));
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) await limits.WaitAsync(true, default);
        var eleventh = limits.WaitAsync(true, default);
        PortAssert.False(eleventh.IsCompleted);
        for (var i = 0; i < 40; i++) await limits.WaitAsync(false, default);
        await eleventh;
        PortAssert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(850));
    }
    [Test]
    public async Task Token_wait_is_bounded_and_never_bypassed()
    {
        using var limits = new AnalysisRateLimits(Options.Create(new DocumentIntelligenceOptions { AnalyzePerSecond = 1, TokenWaitTimeout = TimeSpan.FromMilliseconds(25) }));
        await limits.WaitAsync(true, default);
        var error = await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => limits.WaitAsync(true, default));
        PortAssert.Equal(AnalysisUnavailableReason.RateLimited, error.Reason);
    }
    [Test]
    public async Task Rest_client_reserves_post_bucket_before_transport()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(400)); });
        for (var i = 0; i < 10; i++) await h.Client.ReserveSubmitAsync(default);
        var next = h.Client.ReserveSubmitAsync(default);
        PortAssert.False(next.IsCompleted); PortAssert.Equal(0, calls);
        var permit = await next;
        await h.Client.SubmitAsync(permit, OperationKind.Extract, "m", new byte[] { 1 }, default);
        PortAssert.Equal(1, calls);
    }
    [Test]
    public async Task Full_post_bucket_fails_reserve_but_existing_permit_still_sends_once()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(400)); },
            new() { AnalyzePerSecond = 1, TokenWaitTimeout = TimeSpan.FromMilliseconds(25) });
        var permit = await h.Client.ReserveSubmitAsync(default);
        var error = await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.ReserveSubmitAsync(default));
        PortAssert.Equal(AnalysisUnavailableReason.RateLimited, error.Reason); PortAssert.Equal(0, calls);
        // Force the cache into its refresh window and fail the credential: submit must use its captured token.
        h.Clock.Now = h.Clock.Now.AddMinutes(56); h.Credential.Fail = true;
        PortAssert.IsType<SubmitResult.Rejected>(await h.Client.SubmitAsync(permit, OperationKind.Extract, "m", new byte[] { 1 }, default));
        PortAssert.Equal(1, calls); PortAssert.Equal(1, h.Credential.Calls);
        await PortAssert.ThrowsAsync<InvalidOperationException>(() => h.Client.SubmitAsync(permit, OperationKind.Extract, "m", new byte[] { 1 }, default));
        PortAssert.Equal(1, calls);
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Credential_failure_is_local_unavailable_without_transport(bool submit)
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(400)); });
        h.Credential.Fail = true;
        var error = submit
            ? await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.ReserveSubmitAsync(default))
            : await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.PollAsync(Operation, default));
        PortAssert.Equal(AnalysisUnavailableReason.CredentialUnavailable, error.Reason);
        PortAssert.Null(error.InnerException); PortAssert.Equal(0, calls);
    }
    [Test]
    public async Task Full_get_bucket_fails_poll_before_transport()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { calls++; return Task.FromResult(Response(200, """{"status":"running"}""")); },
            new() { PollPerSecond = 1, TokenWaitTimeout = TimeSpan.FromMilliseconds(25) });
        await h.Client.PollAsync(Operation, default);
        var error = await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.PollAsync(Operation, default));
        PortAssert.Equal(AnalysisUnavailableReason.RateLimited, error.Reason); PortAssert.Equal(1, calls);
    }
    [Test]
    public async Task Concurrent_permit_reuse_reaches_transport_once()
    {
        var calls = 0;
        using var h = new Harness((_, _) => { Interlocked.Increment(ref calls); return Task.FromResult(Response(400)); });
        var permit = await h.Client.ReserveSubmitAsync(default);
        var attempts = Enumerable.Range(0, 10).Select(async _ =>
        {
            try { await h.Client.SubmitAsync(permit, OperationKind.Extract, "m", new byte[] { 1 }, default); return true; }
            catch (InvalidOperationException) { return false; }
        });
        PortAssert.Single(await Task.WhenAll(attempts), sent => sent); PortAssert.Equal(1, calls);
    }
    [Test]
    public async Task Fresh_cached_token_does_not_wait_for_refresh_gate()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(200, """{"status":"running"}""")));
        await h.Client.PollAsync(Operation, default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Credential.BeforeReturn = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        var initialTime = h.Clock.Now;
        h.Clock.Now = initialTime.AddMinutes(55);
        var refresh = h.Client.PollAsync(Operation, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // A wall-clock adjustment makes the published token fresh while the refresh owns the gate.
        h.Clock.Now = initialTime.AddMinutes(54);
        try { await h.Client.PollAsync(Operation, default).WaitAsync(TimeSpan.FromSeconds(2)); }
        finally { release.TrySetResult(); await refresh; }
        PortAssert.Equal(2, h.Credential.Calls);
    }
    [Test]
    public async Task Full_reservation_queue_returns_dedicated_reason_and_caller_can_cancel_wait()
    {
        using var h = new Harness((_, _) => Task.FromResult(Response(400)),
            new() { AnalyzePerSecond = 1, QueueLimit = 1 });
        await h.Client.ReserveSubmitAsync(default);
        using var cts = new CancellationTokenSource();
        var waiting = h.Client.ReserveSubmitAsync(cts.Token);
        PortAssert.False(waiting.IsCompleted);
        try
        {
            var error = await PortAssert.ThrowsAsync<AnalysisUnavailableException>(() => h.Client.ReserveSubmitAsync(default));
            PortAssert.Equal(AnalysisUnavailableReason.RateLimited, error.Reason);
        }
        finally { cts.Cancel(); }
        await PortAssert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
