using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Sql.DocAi;

internal sealed class SqlExecution(ISqlConnectionFactory connections, SqlStatementCatalog sql, IOptions<DocAiSqlOptions> options, ILogger? logger = null)
{
    static SqlExecution() => SqlMapper.AddTypeHandler(new UtcTimestampHandler());
    internal CommandDefinition Command(string label, object? values, CancellationToken ct) =>
        new(sql[label], new SqlParameters(values), commandTimeout: label == "S15" ? options.Value.RetentionTimeoutSeconds : label is "S27" or "S28" ? Math.Clamp(options.Value.CommandTimeoutSeconds, 1, 120) : options.Value.CommandTimeoutSeconds, cancellationToken: ct);

    internal async Task<T?> One<T>(string label, object? values, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<T>(Command(label, values, ct));
    }

    internal async Task<IReadOnlyList<T>> Many<T>(string label, object? values, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return (await connection.QueryAsync<T>(Command(label, values, ct))).AsList();
    }

    internal async Task<int> Execute(string label, object? values, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        connection.InfoMessage += (_, message) =>
        {
            if (message.Errors.Cast<SqlError>().Any(e => e.Message == "page_quota.completed_without_billed_usage"))
                logger?.LogError("{event}", "page_quota.completed_without_billed_usage");
        };
        return await connection.ExecuteAsync(Command(label, values, ct));
    }

    internal async Task<WriteOutcome> Write(string label, object values, bool scalar, CancellationToken ct)
    {
        try
        {
            var count = scalar ? await One<int>(label, values, ct) : await Execute(label, values, ct);
            return count > 0 ? WriteOutcome.Applied : WriteOutcome.LeaseLost;
        }
        catch (SqlException e) when (e.Number is 50010 or 50012 or 50013)
        {
            return e.Number switch { 50012 => WriteOutcome.OperationNotOpen, 50013 => WriteOutcome.RetriesExhausted, _ => WriteOutcome.LeaseLost };
        }
    }
}
