using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Application.Common.Interfaces.DocAi;
public interface IWorkerCircuitBreaker
{
    TimeSpan Remaining { get; }
    Task<ClaimedJob?> ClaimAsync(IJobQueue queue, CancellationToken ct);
    void Open();
}
