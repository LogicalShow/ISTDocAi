using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.RetryPolicyService;
public sealed class RetryPolicyService : IRetryPolicyService
{
    /// <summary>[S6]/[S9]: attempts is the number of failures already recorded, not the next attempt.</summary>
    public Decision<RetryDecision> Failure(int attempts, TimeSpan? retryAfter = null)
    {
        if (attempts is < 0 or > 4 || retryAfter < TimeSpan.Zero)
            return Decision<RetryDecision>.Reject(Rejection.InvalidInput, "Expected 0..4 prior failures and a nonnegative delay.");
        if (attempts == 4)
            return Decision<RetryDecision>.Accept(new(RetryAction.Fail, 5, TimeSpan.Zero, ErrorCodes.RetriesExhausted));
        var backoff = TimeSpan.FromSeconds(15 * (1 << (attempts + 1)));
        return Decision<RetryDecision>.Accept(new(RetryAction.Requeue, attempts + 1, retryAfter > backoff ? retryAfter.Value : backoff));
    }

    /// <summary>[S5]/[S7]: a running poll's Retry-After is a wait, not a retryable HTTP failure.</summary>
    public Decision<RetryDecision> PollWait(int attempts, TimeSpan retryAfter) => attempts is < 0 or > 4 || retryAfter < TimeSpan.Zero ? Decision<RetryDecision>.Reject(Rejection.InvalidInput, "Invalid attempts or polling delay.") : Decision<RetryDecision>.Accept(new(retryAfter > TimeSpan.FromMinutes(5) ? RetryAction.Release : RetryAction.Wait, attempts, retryAfter));
    public RetryDecision Configuration(int attempts) => new(RetryAction.Release, attempts, TimeSpan.FromMinutes(5), ErrorCodes.AzureAuth);
    public RetryDecision LostLease(int attempts) => new(RetryAction.Stop, attempts, TimeSpan.Zero);
}
