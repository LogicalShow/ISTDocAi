using ISTDocAi.Application.Common.Contracts.DocAi.V1;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IUsageReader
{
    Task<TopUpCyclesResponse?> GetTopUpCyclesAsync(short consumerId, long? grantId, CancellationToken ct);
    Task<CycleDocumentsResponse?> GetCycleDocumentsAsync(short consumerId, long grantId, int limit, long? afterChargeId, CancellationToken ct);
    Task<UsageResponse> GetUsageAsync(short consumerId, int months, DateTimeOffset asOfUtc, CancellationToken ct);
}
