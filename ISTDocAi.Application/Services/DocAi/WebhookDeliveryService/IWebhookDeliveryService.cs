using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;

namespace ISTDocAi.Application.Services.DocAi.WebhookDeliveryService;
public interface IWebhookDeliveryService
{
    Decision<WebhookDeliverySnapshot> Create(int runNo, DateTimeOffset now);
    Decision<WebhookDeliverySnapshot> Claim(WebhookDeliverySnapshot entity, Guid newToken, DateTimeOffset now);
    Decision<WebhookDeliverySnapshot> RecordAttempt(WebhookDeliverySnapshot entity, Guid token, int? statusCode, string? error, DateTimeOffset now);
    Decision<WebhookDeliverySnapshot> Replay(WebhookDeliverySnapshot entity, int currentRunNo, DateTimeOffset now);
    Decision<WebhookDeliverySnapshot> Supersede(WebhookDeliverySnapshot entity, int newRunNo);
}
