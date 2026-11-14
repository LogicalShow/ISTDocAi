using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.RetryPolicyService;
public interface IRetryPolicyService
{
    Decision<RetryDecision> Failure(int attempts, TimeSpan? retryAfter = null);
    Decision<RetryDecision> PollWait(int attempts, TimeSpan retryAfter);
    RetryDecision Configuration(int attempts);
    RetryDecision LostLease(int attempts);
}
