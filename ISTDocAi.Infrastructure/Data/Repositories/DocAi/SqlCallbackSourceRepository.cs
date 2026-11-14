using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlCallbackSourceRepository : ICallbackSource
{
    private readonly SqlExecution execution;
    internal SqlCallbackSourceRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<IReadOnlyList<CallbackConsumer>> GetCallbacksAsync(CancellationToken ct) => execution.Many<CallbackConsumer>("S23", null, ct);

}
