using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
public sealed class WebhookPolicyService : IWebhookPolicyService
{
    public TimeSpan DelayAfterAttempt(int attempt) => TimeSpan.FromMinutes(attempt switch
    {
        1 => 1,
        2 => 5,
        3 => 15,
        4 => 60,
        _ => 360
    });
    public bool IsDelivered(int? statusCode) => statusCode is >= 200 and <= 299;
}
