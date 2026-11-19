using ISTDocAi.Application.Services.DocAi.Pricing;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Pricing;
public interface IAzureRetailPriceSource
{
    Task<IReadOnlyList<AzurePriceObservation>> ReadAsync(CancellationToken cancellationToken);
}
