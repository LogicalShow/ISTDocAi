using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Data.StoredProcedures;

public sealed class StoredProcedureExecutor(AppDbContext context) : IStoredProcedureExecutor
{
    private static readonly Regex ProcedureNamePattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ParameterNamePattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<TResult>> QueryAsync<TResult>(
        string procedureName,
        IReadOnlyCollection<StoredProcedureParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(procedureName, parameters);

        return await context.Database
            .SqlQueryRaw<TResult>(command.Sql, command.Parameters.Cast<object>().ToArray())
            .ToListAsync(cancellationToken);
    }

    public Task<int> ExecuteAsync(
        string procedureName,
        IReadOnlyCollection<StoredProcedureParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(procedureName, parameters);

        return context.Database.ExecuteSqlRawAsync(
            command.Sql,
            command.Parameters.Cast<object>(),
            cancellationToken);
    }

    private (string Sql, DbParameter[] Parameters) CreateCommand(
        string procedureName,
        IReadOnlyCollection<StoredProcedureParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var qualifiedProcedureName = QuoteProcedureName(procedureName);
        using var parameterFactory = context.Database.GetDbConnection().CreateCommand();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbParameters = new List<DbParameter>(parameters.Count);

        foreach (var parameter in parameters)
        {
            var parameterName = NormalizeParameterName(parameter.Name);
            if (!names.Add(parameterName))
                throw new ArgumentException($"Duplicate stored-procedure parameter '{parameterName}'.", nameof(parameters));

            var dbParameter = parameterFactory.CreateParameter();
            dbParameter.ParameterName = parameterName;
            dbParameter.Value = parameter.Value ?? DBNull.Value;

            if (parameter.DbType.HasValue)
                dbParameter.DbType = parameter.DbType.Value;

            if (parameter.Size.HasValue)
                dbParameter.Size = parameter.Size.Value;

            dbParameters.Add(dbParameter);
        }

        var arguments = string.Join(", ", dbParameters.Select(x => x.ParameterName));
        var sql = arguments.Length == 0
            ? $"EXEC {qualifiedProcedureName}"
            : $"EXEC {qualifiedProcedureName} {arguments}";

        return (sql, dbParameters.ToArray());
    }

    private static string QuoteProcedureName(string procedureName)
    {
        if (string.IsNullOrWhiteSpace(procedureName) || !ProcedureNamePattern.IsMatch(procedureName))
            throw new ArgumentException("Use a procedure name such as 'usp_Report' or 'dbo.usp_Report'.", nameof(procedureName));

        return string.Join(".", procedureName.Split('.').Select(part => $"[{part}]"));
    }

    private static string NormalizeParameterName(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            throw new ArgumentException("Stored-procedure parameter names cannot be empty.", nameof(parameterName));

        var nameWithoutPrefix = parameterName.Trim().TrimStart('@');
        if (!ParameterNamePattern.IsMatch(nameWithoutPrefix))
            throw new ArgumentException($"Invalid stored-procedure parameter name '{parameterName}'.", nameof(parameterName));

        return $"@{nameWithoutPrefix}";
    }
}
