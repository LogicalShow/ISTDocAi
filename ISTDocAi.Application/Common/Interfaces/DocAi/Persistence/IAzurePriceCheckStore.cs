using ISTDocAi.Application.Services.DocAi.Pricing;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
public interface IAzurePriceCheckStore
{
    Task<DateTimeOffset?> LatestCompletedAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AzurePriceCheckResult>> SaveAsync(AzurePriceCheck check, CancellationToken cancellationToken);
}
