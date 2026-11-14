using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
namespace ISTDocAi.Application.Services.DocAi.Webhooks;

public sealed class DeliverWebhook(IWebhookOutbox outbox, ICallbackDirectory callbacks, ISecretStore secrets,
    IWebhookTransport transport, TimeProvider clock, SecretFailureLog failures, ILogger<DeliverWebhook> logger, IWebhookPolicyService webhookPolicy, IDocAiV2Json json)
{
    public const int MaximumBodyBytes = 1024 * 1024;
    public async Task ExecuteAsync(ClaimedDelivery delivery)
    {
        var started = clock.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object?> {
            ["deliveryId"] = delivery.DeliveryId, ["eventId"] = delivery.EventId, ["jobId"] = delivery.JobId,
            ["runNo"] = delivery.RunNo, ["consumerId"] = delivery.ConsumerId, ["attempt"] = delivery.Attempts + 1 });
        IDisposable? correlation = null;
        var consumerResolved = false;
        var recordingStarted = false;
        try
        {
            using var preparation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var context = await outbox.GetEventContextAsync(delivery.JobId, delivery.RunNo, preparation.Token)
                ?? throw new InvalidOperationException("Missing event context.");
            correlation = logger.BeginScope(new Dictionary<string, object?> { ["correlationId"] = context.CorrelationId });
            var consumer = (await callbacks.ReadAsync(preparation.Token)).SingleOrDefault(c => c.ConsumerId == delivery.ConsumerId);
            consumerResolved = consumer is not null;
            if (consumer is null || !IsCallbackUrl(consumer.CallbackUrl)) { await Record(null, "NO_CALLBACK"); return; }
            var body = BuildBody(delivery.PayloadJson, context.ResultJson, json);
            if (body.Length > MaximumBodyBytes)
            {
                logger.LogError("{event}", "webhook.body_too_large");
                await Record(null, "BODY_TOO_LARGE"); return;
            }
            var key = await secrets.GetSigningKeyAsync(consumer.WebhookSecretName, preparation.Token);
            var ca = consumer.CallbackCaCertName is null ? null :
                await secrets.GetCaCertificatePemAsync(consumer.CallbackCaCertName, preparation.Token);
            var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            WebhookResponse response;
            using (var send = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    response = await transport.SendAsync(new(consumer, body, timestamp,
                        WebhookSignature.Compute(key, timestamp, body), context.CorrelationId, ca), send.Token);
                }
                catch (OperationCanceledException) { response = new(null, WebhookFailure.Timeout); }
            }
            if (response.StatusCode == 401) secrets.InvalidateSigningKey(consumer.WebhookSecretName);
            var error = response.StatusCode is { } status ? webhookPolicy.IsDelivered(status) ? null : $"HTTP {status}" :
                response.Failure switch { WebhookFailure.Timeout => "TIMEOUT", WebhookFailure.TlsFailed => "TLS_FAILED", _ => "CONNECTION_FAILED" };
            await Record(response.StatusCode, error);
        }
        catch (SecretUnavailableException e) { failures.Report(e.SecretName); }
        catch (Exception)
        {
            logger.LogError("{event}", recordingStarted ? "webhook.record_failed" : "webhook.processing_error");
            // Never retry an ambiguous S11 write: it may have committed before the exception.
            if (consumerResolved && !recordingStarted)
            {
                try { await Record(null, "INTERNAL_ERROR"); }
                catch (Exception) { logger.LogError("{event}", "webhook.record_failed"); }
            }
        }
        finally { correlation?.Dispose(); }

        async Task Record(short? status, string? error)
        {
            recordingStarted = true;
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var outcome = await outbox.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, status, error, budget.Token);
            if (outcome != WriteOutcome.Applied) { logger.LogInformation("{event}", "webhook.lease_lost"); return; }
            var delivered = webhookPolicy.IsDelivered(status);
            logger.Log(delivered ? LogLevel.Information : LogLevel.Warning,
                "{event} statusCode={statusCode} errorCode={errorCode} durationMs={durationMs}",
                delivered ? "webhook.delivered" : "webhook.failed", status, error, clock.GetElapsedTime(started).TotalMilliseconds);
            if (!delivered && delivery.Attempts + 1 >= 8) logger.LogError("{event}", AlertEvents.WebhookDeadLettered);
        }
    }

    public static bool IsCallbackUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    public static byte[] BuildBody(string payloadJson, string? resultJson, IDocAiV2Json json)
    {
        var payload = JsonNode.Parse(payloadJson) as JsonObject ?? throw new InvalidOperationException("Invalid event payload.");
        if (!payload.ContainsKey("error")) payload["error"] = null;
        var resultType = payload["resultType"]?.GetValue<string>();
        CanonicalResultCodec.ValidateType(resultType);
        if (resultJson is not null && (resultType is not null || payload.ContainsKey("requestedDocumentType")))
        {
            var result = CanonicalResultCodec.Deserialize(resultJson, resultType);
            if (payload["documentType"]?.GetValue<string>() is { } type && type != result.DocumentTypeCode)
                throw new InvalidOperationException("Result and event document types disagree.");
        }
        return WebhookBodyProjector.Project(payload, resultJson, json.Options);
    }
}
