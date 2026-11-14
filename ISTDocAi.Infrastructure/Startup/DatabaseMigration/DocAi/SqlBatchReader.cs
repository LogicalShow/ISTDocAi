using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Text.RegularExpressions;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

public static class SqlBatchReader
{
    // Matches the existing SQL test helpers: only standalone GO, case insensitive.
    public static IEnumerable<string> Split(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(batch => !string.IsNullOrWhiteSpace(batch));
    }

    public static string ReadResource(string fileName)
    {
        using var stream = typeof(SqlBatchReader).Assembly.GetManifestResourceStream(
            "DocAi.Database.Migrations.Sql." + fileName)
            ?? throw new InvalidOperationException("Missing frozen migration resource: " + fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string PrepareBatch(string batch)
    {
        // Dynamic SQL keeps module DDL first in its batch inside EF idempotency guards.
        return Regex.IsMatch(batch, @"\bCREATE\s+(?:OR\s+ALTER\s+)?(?:PROC(?:EDURE)?|VIEW|TRIGGER|FUNCTION)\b",
            RegexOptions.IgnoreCase)
            ? "EXEC(N'" + batch.Replace("'", "''", StringComparison.Ordinal) + "');"
            : batch;
    }
}
