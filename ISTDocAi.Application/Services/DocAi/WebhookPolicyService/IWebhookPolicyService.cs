using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
public interface IWebhookPolicyService
{
    TimeSpan DelayAfterAttempt(int attempt);
    bool IsDelivered(int? statusCode);
}
