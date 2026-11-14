using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlRetentionRepository : IRetention
{
    private readonly SqlExecution execution;
    internal SqlRetentionRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public async Task RunAsync(CancellationToken cancellationToken) => _ = await execution.Execute("S15", null, cancellationToken);

}
