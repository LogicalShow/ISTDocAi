using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlModelThresholdSourceRepository : IModelThresholdSource
{
    private readonly SqlExecution execution;
    internal SqlModelThresholdSourceRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<decimal?> GetThresholdAsync(string modelId, CancellationToken ct) => execution.One<decimal?>("S22", new { modelId }, ct);

}
