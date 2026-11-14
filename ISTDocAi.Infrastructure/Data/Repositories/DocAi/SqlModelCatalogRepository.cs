using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlModelCatalogRepository : IModelCatalog
{
    private readonly SqlExecution execution;
    internal SqlModelCatalogRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public async Task ActivateAsync(string newModelId, decimal minimumAccuracy, CancellationToken cancellationToken) =>
        _ = await execution.Execute("S14", new { newModelId, minimumAccuracy }, cancellationToken);

    public Task<IReadOnlyList<CatalogModel>> GetCatalogAsync(CancellationToken cancellationToken) => execution.Many<CatalogModel>("S16", null, cancellationToken);

}
