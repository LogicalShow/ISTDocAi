using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlSignalReaderRepository : ISignalReader
{
    private readonly SqlExecution execution;
    internal SqlSignalReaderRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<int?> OldestDueSecondsAsync(CancellationToken ct) => execution.One<int?>("S24", null, ct);

    public Task<IReadOnlyList<UnpricedUsage>> UnpricedUsageAsync(CancellationToken ct) => execution.Many<UnpricedUsage>("B9", null, ct);

}
