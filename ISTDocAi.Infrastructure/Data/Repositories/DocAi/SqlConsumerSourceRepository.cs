using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlConsumerSourceRepository : IConsumerSource
{
    private readonly SqlExecution execution;
    internal SqlConsumerSourceRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<IReadOnlyList<Consumer>> GetConsumersAsync(CancellationToken cancellationToken) => execution.Many<Consumer>("S21", null, cancellationToken);

}
