namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IJobQueue
{
    Task<ClaimedJob?> ClaimAsync(CancellationToken cancellationToken);
    Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken cancellationToken);
    Task<WriteOutcome> RetryAsync(Lease lease, int retryAfterSeconds, string? errorCode, CancellationToken cancellationToken);
    Task<WriteOutcome> ReleaseAsync(Lease lease, int delaySeconds, string? errorCode, CancellationToken cancellationToken);
}
