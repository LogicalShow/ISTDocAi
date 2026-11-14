using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;

namespace ISTDocAi.Application.Services.DocAi.WebhookDeliveryService;
public sealed class WebhookDeliveryService(IWebhookPolicyService _webhookPolicyService) : IWebhookDeliveryService
{
    public Decision<WebhookDeliverySnapshot> Create(int runNo, DateTimeOffset now) => runNo is < 1 or > short.MaxValue ? Reject(Rejection.InvalidInput, "Invalid SQL run number.") : Decision<WebhookDeliverySnapshot>.Accept(new() { RunNo = runNo, Status = WebhookDeliveryStatus.Pending, NextAttemptAtUtc = now });
    public Decision<WebhookDeliverySnapshot> Claim(WebhookDeliverySnapshot entity, Guid newToken, DateTimeOffset now)
    {
        if (newToken == Guid.Empty || newToken == entity.LeaseToken)
            return Reject(Rejection.InvalidInput, "A fresh lease token is required.");
        if (entity.Status != WebhookDeliveryStatus.Pending || entity.NextAttemptAtUtc > now || entity.LeaseExpiresAtUtc >= now)
            return Reject(Rejection.NotDue, "Delivery is not Pending and due with an available lease.");
        return Decision<WebhookDeliverySnapshot>.Accept(entity with { LeaseToken = newToken, LeaseExpiresAtUtc = now.AddMinutes(5) });
    }

    public Decision<WebhookDeliverySnapshot> RecordAttempt(WebhookDeliverySnapshot entity, Guid token, int? statusCode, string? error, DateTimeOffset now)
    {
        if (entity.Status != WebhookDeliveryStatus.Pending || token == Guid.Empty || entity.LeaseToken != token)
            return Reject(Rejection.LeaseLost, "Delivery lease lost: stop and write nothing.");
        var attempt = entity.Attempts + 1;
        var delivered = _webhookPolicyService.IsDelivered(statusCode);
        // Persist the scheduled retry kind in the existing error field, matching S11.
        var immediate = statusCode is null && error is "CONNECTION_FAILED" or "TIMEOUT" or "TLS_FAILED"
            && attempt < 8 && !(entity.LastError?.EndsWith(":IMMEDIATE", StringComparison.Ordinal) ?? false);
        return Decision<WebhookDeliverySnapshot>.Accept(entity with { Attempts = attempt, LastStatusCode = statusCode, LastError = immediate ? error + ":IMMEDIATE" : error, LeaseToken = null, LeaseExpiresAtUtc = null, Status = delivered ? WebhookDeliveryStatus.Delivered : attempt >= 8 ? WebhookDeliveryStatus.DeadLettered : WebhookDeliveryStatus.Pending, DeliveredAtUtc = delivered ? now : null, NextAttemptAtUtc = immediate ? now : now + _webhookPolicyService.DelayAfterAttempt(attempt) });
    }

    public Decision<WebhookDeliverySnapshot> Replay(WebhookDeliverySnapshot entity, int currentRunNo, DateTimeOffset now) => entity.Status != WebhookDeliveryStatus.DeadLettered || currentRunNo != entity.RunNo ? Reject(Rejection.InvalidTransition, "Only a DeadLettered delivery of the current run may replay.") : Decision<WebhookDeliverySnapshot>.Accept(entity with { Status = WebhookDeliveryStatus.Pending, Attempts = 0, NextAttemptAtUtc = now, LeaseToken = null, LeaseExpiresAtUtc = null, LastError = null });
    public Decision<WebhookDeliverySnapshot> Supersede(WebhookDeliverySnapshot entity, int newRunNo) => entity.Status != WebhookDeliveryStatus.Pending || newRunNo <= entity.RunNo ? Reject(Rejection.InvalidTransition, "Only older Pending events are superseded [S2].") : Decision<WebhookDeliverySnapshot>.Accept(entity with { Status = WebhookDeliveryStatus.Superseded, LeaseToken = null, LeaseExpiresAtUtc = null });
    private Decision<WebhookDeliverySnapshot> Reject(Rejection reason, string detail) => Decision<WebhookDeliverySnapshot>.Reject(reason, detail);
}
