using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;

using System.Text;
using System.Text.Json.Nodes;

using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class DeliverWebhookTests
{
    
    [TestCase(true)] [TestCase(false)]
    public async Task Body_preserves_order_and_always_carries_error_and_result(bool completed)
    {
        var f = new WebhookFakes();
        if (!completed) f.Context = f.Context with { ResultJson = null };
        await f.Processor().ExecuteAsync(f.Delivery);
        var request = Assert.Single(f.Requests);
        var body = JsonNode.Parse(request.Body)!.AsObject();
        Assert.Equal(new[] { "EventId", "EventType", "OccurredAtUtc", "Job" }, body.Select(p => p.Key));
        Assert.True(body["Job"]!.AsObject().ContainsKey("Error")); Assert.Null(body["Job"]!["Error"]);
        if (completed) Assert.Equal("PRIVATE_VALUE", body["Job"]!["Result"]!["privateField"]!.GetValue<string>());
        else Assert.Null(body["Job"]!["Result"]);
        Assert.False(request.Body.AsSpan().StartsWith(new byte[] { 239, 187, 191 }));
        Assert.Equal(f.Context.CorrelationId, request.CorrelationId);
        Assert.True(WebhookSignature.Verify(request.Timestamp, request.Body, request.Signature, f.Key));
        Assert.Equal(((short?)200, (string?)null), Assert.Single(f.Attempts));
    }
    
    [TestCase(1, "1793268922", "4dad37deda733dbf91e6907cd9b718ef45ae55cd6b44a4f382e32258f3d31e14")]
    [TestCase(2, "1793271763", "4489eab28971b0bd17ccc6b66e0e2eba5b12b33c7ddc9d690686afbad068b168")]
    public async Task Both_vectors_sign_exact_sent_bytes_and_retry_has_a_fresh_timestamp(int vector, string timestamp, string hex)
    {
        var f = new WebhookFakes();
        f.Delivery = f.Delivery with { PayloadJson = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Stage9", $"hmac-vector-{vector}-body.json")) };
        f.Context = f.Context with { ResultJson = null };
        f.Clock.Advance(DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp)) - f.Clock.GetUtcNow());
        var processor = f.Processor();
        await processor.ExecuteAsync(f.Delivery);
        Assert.Equal(DeliverWebhook.BuildBody(f.Delivery.PayloadJson, null, new DocAiV2Json()), f.Requests[0].Body);
        Assert.Equal("sha256=" + hex, WebhookSignature.Compute(f.Key, timestamp, Encoding.UTF8.GetBytes(f.Delivery.PayloadJson)));
        Assert.Equal(WebhookSignature.Compute(f.Key, timestamp, f.Requests[0].Body), f.Requests[0].Signature);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await processor.ExecuteAsync(f.Delivery);
        Assert.NotEqual(f.Requests[0].Timestamp, f.Requests[1].Timestamp);
        Assert.All(f.Requests, r => Assert.True(WebhookSignature.Verify(r.Timestamp, r.Body, r.Signature, f.Key)));
    }
    
    [TestCase(200, null, null)] [TestCase(204, null, null)] [TestCase(299, null, null)]
    [TestCase(302, null, "HTTP 302")] [TestCase(401, null, "HTTP 401")] [TestCase(500, null, "HTTP 500")]
    [TestCase(null, WebhookFailure.Timeout, "TIMEOUT")]
    [TestCase(null, WebhookFailure.ConnectionFailed, "CONNECTION_FAILED")]
    [TestCase(null, WebhookFailure.TlsFailed, "TLS_FAILED")]
    public async Task Outcomes_record_only_service_codes(int? status, WebhookFailure? failure, string? error)
    {
        var f = new WebhookFakes { Response = new((short?)status, failure) };
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Equal(((short?)status, error), Assert.Single(f.Attempts));
        Assert.Equal(status == 401 ? 1 : 0, f.Invalidations);
    }
    
    [TestCase(null)] [TestCase("http://consumer.example")] [TestCase("/relative")]
    [TestCase("https://user@consumer.example")] [TestCase("https://consumer.example/#fragment")]
    public async Task Missing_or_invalid_callbacks_do_not_send(string? url)
    {
        var f = new WebhookFakes(); f.Callback = url is null ? null : f.Callback! with { CallbackUrl = url };
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Empty(f.Requests); Assert.Equal(((short?)null, "NO_CALLBACK"), Assert.Single(f.Attempts));
    }
    [Test]
    public async Task Oversized_body_is_recorded_and_logged_without_sending()
    {
        var f = new WebhookFakes(); f.Context = f.Context with { ResultJson = "{\"value\":\"" + new string('x', 1024 * 1024) + "\"}" };
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Empty(f.Requests); Assert.Equal(((short?)null, "BODY_TOO_LARGE"), Assert.Single(f.Attempts));
        Assert.Contains(f.Logs.Lines, l => l.Level == LogLevel.Error && l.Text.Contains("webhook.body_too_large"));
    }
    [Test]
    public async Task Exactly_one_MiB_is_sent_and_one_more_byte_is_refused()
    {
        var f = new WebhookFakes();
        var empty = DeliverWebhook.BuildBody(f.Delivery.PayloadJson, "{\"value\":\"\"}", new DocAiV2Json()).Length;
        f.Context = f.Context with { ResultJson = "{\"value\":\"" + new string('x', DeliverWebhook.MaximumBodyBytes - empty) + "\"}" };
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Equal(DeliverWebhook.MaximumBodyBytes, Assert.Single(f.Requests).Body.Length);
        f.Context = f.Context with { ResultJson = f.Context.ResultJson!.Replace("x\"}", "xx\"}", StringComparison.Ordinal) };
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Single(f.Requests); Assert.Equal("BODY_TOO_LARGE", f.Attempts.Last().Error);
    }
    
    [TestCase(6, 500, false)] [TestCase(7, 500, true)] [TestCase(7, 200, false)]
    public async Task Only_eighth_failure_logs_dead_letter(byte attempts, short status, bool deadLetter)
    {
        var f = new WebhookFakes { Response = new(status) };
        await f.Processor().ExecuteAsync(f.Delivery with { Attempts = attempts });
        Assert.Equal(deadLetter, f.Logs.Lines.Any(l => l.Level == LogLevel.Error && l.Text.Contains("webhook.dead_lettered")));
    }
    [Test]
    public async Task KeyVault_failure_spends_no_attempt_and_critical_is_throttled_across_scopes()
    {
        var f = new WebhookFakes { SecretFailure = true };
        await f.Processor().ExecuteAsync(f.Delivery); await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Empty(f.Requests); Assert.Empty(f.Attempts);
        Assert.Single(f.SecretLogs.Lines, l => l.Level == LogLevel.Critical);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Equal(2, f.SecretLogs.Lines.Count(l => l.Level == LogLevel.Critical));
    }
    [Test]
    public async Task Lease_lost_logs_information_without_delivered_or_dead_letter()
    {
        var f = new WebhookFakes { Write = WriteOutcome.LeaseLost, Response = new(500) };
        await f.Processor().ExecuteAsync(f.Delivery with { Attempts = 7 });
        Assert.Single(f.Logs.Lines); Assert.Contains("webhook.lease_lost", f.Logs.Lines.First().Text);
        Assert.Equal(LogLevel.Information, f.Logs.Lines.First().Level);
    }
    [Test]
    public async Task Unexpected_delivery_failure_is_contained_and_no_sensitive_data_is_logged()
    {
        var f = new WebhookFakes();
        await f.Processor().ExecuteAsync(f.Delivery);
        var r = f.Requests[0];
        f.Send = (_, _) => throw new IOException(Encoding.UTF8.GetString(r.Body) + r.Signature + f.Callback!.CallbackUrl);
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Equal(((short?)null, "INTERNAL_ERROR"), f.Attempts.Last());
        var logs = string.Join("\n", f.Logs.Lines.Select(l => l.Text));
        foreach (var sensitive in new[] { "PRIVATE_VALUE", Convert.ToBase64String(f.Key), r.Signature, f.Callback!.CallbackUrl, Encoding.UTF8.GetString(r.Body) }) Assert.DoesNotContain(sensitive, logs);
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Unexpected_failure_after_consumer_resolution_records_INTERNAL_ERROR_and_can_dead_letter(bool beforePost)
    {
        var f = new WebhookFakes();
        if (beforePost) f.Delivery = f.Delivery with { PayloadJson = "{" };
        else f.Send = (_, _) => throw new InvalidOperationException("PRIVATE_CONSUMER_TEXT");
        await f.Processor().ExecuteAsync(f.Delivery with { Attempts = 7 });
        Assert.Equal(((short?)null, "INTERNAL_ERROR"), Assert.Single(f.Attempts));
        Assert.Equal(beforePost ? 0 : 1, f.Requests.Count);
        Assert.False(f.RecordWasCancelled);
        Assert.Contains(f.Logs.Lines, l => l.Level == LogLevel.Error && l.Text.Contains("webhook.dead_lettered"));
        Assert.DoesNotContain(f.Logs.Lines, l => l.Text.Contains("PRIVATE_CONSUMER_TEXT"));
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Failing_record_is_logged_and_does_not_escape_or_trigger_another_write(bool recovery)
    {
        var f = new WebhookFakes();
        if (recovery) f.Send = (_, _) => throw new InvalidOperationException("PRIVATE_SEND_TEXT");
        f.Record = _ => throw new IOException("PRIVATE_SQL_TEXT");
        await f.Processor().ExecuteAsync(f.Delivery);
        var attempted = Assert.Single(f.Attempts);
        Assert.Equal(recovery ? ((short?)null, "INTERNAL_ERROR") : ((short?)200, (string?)null), attempted);
        Assert.Contains(f.Logs.Lines, l => l.Level == LogLevel.Error && l.Text.Contains("webhook.record_failed"));
        Assert.DoesNotContain(f.Logs.Lines, l => l.Text.Contains("PRIVATE_"));
        Assert.False(f.RecordWasCancelled);
    }
    [Test]
    public async Task Readiness_needs_no_secret_without_callbacks_and_accepts_a_readable_signing_secret()
    {
        var f = new WebhookFakes { Callback = null };
        var readiness = new WebhookReadiness(f, f, f.FailureLog);
        Assert.True(await readiness.CheckAsync(default)); Assert.Equal(0, f.SecretReads);
        f.Callback = new(1, "TEST", "https://test.example", "secret", null);
        Assert.True(await readiness.CheckAsync(default));
        f.SecretFailure = true; Assert.False(await readiness.CheckAsync(default));
    }
}
