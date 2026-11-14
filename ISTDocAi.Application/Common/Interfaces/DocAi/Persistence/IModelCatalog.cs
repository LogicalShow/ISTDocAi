namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IModelCatalog
{
    Task ActivateAsync(string newModelId, decimal minimumAccuracy, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogModel>> GetCatalogAsync(CancellationToken cancellationToken);
}
