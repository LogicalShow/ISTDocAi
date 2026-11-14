using Microsoft.EntityFrameworkCore.Migrations;

namespace ISTDocAi.Infrastructure.Utilities;

/// <summary>
/// MigrationBuilder extensions to execute embedded SQL scripts and manage programmability objects.
///
/// Primary usage:
/// - Put scripts under Database/Programmability/... as EmbeddedResource.
/// - Keep scripts versioned: usp_X.v1.sql, usp_X.v2.sql, etc.
/// - Call SqlFromEmbedded(...) from migrations to apply the exact version.
/// 
/// Why versioning matters:
/// - If a migration references v1, the content must never change later.
/// - This makes rebuilds reproducible and migrations immutable.
///
/// Notes:
/// - suppressTransaction should be true only when SQL Server forbids transaction usage
///   for that statement (rare; use carefully).
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Loads each embedded SQL script (by suffix) and registers it as a migration SQL operation.
    /// Order matters; pass scripts in dependency order (functions -> views -> procedures).
    /// </summary>
    public static MigrationBuilder SqlFromEmbedded(
        this MigrationBuilder migrationBuilder,
        string[] resourceEndsWith,
        bool suppressTransaction = false)
    {
        foreach (var suffix in resourceEndsWith)
        {
            var sql = SqlScriptLoader.LoadScript(suffix);
            migrationBuilder.Sql(sql, suppressTransaction);
        }
        return migrationBuilder;
    }

    /// <summary>
    /// Convenience overload for running a single embedded SQL script.
    /// </summary>
    public static MigrationBuilder SqlFromEmbedded(
        this MigrationBuilder migrationBuilder,
        string resourceEndsWith,
        bool suppressTransaction = false)
        => migrationBuilder.SqlFromEmbedded(new[] { resourceEndsWith }, suppressTransaction);

    /// <summary>
    /// Drops a stored procedure if it exists.
    /// Accepts either:
    /// - name only: "usp_MyProc" + schema param
    /// - qualified: "dbo.usp_MyProc" (schema param is ignored)
    /// </summary>
    public static MigrationBuilder DropStoredProcedureIfExists(
        this MigrationBuilder migrationBuilder,
        string procedureName,
        string schema = "dbo")
    {
        var (s, n) = NormalizeName(schema, procedureName);

        var sql = $@"
IF OBJECT_ID(N'[{s}].[{n}]', 'P') IS NOT NULL
    DROP PROCEDURE [{s}].[{n}];";

        migrationBuilder.Sql(sql);
        return migrationBuilder;
    }

    /// <summary>
    /// Drops a function if it exists (covers scalar FN, inline table-valued IF, and table-valued TF).
    /// Accepts either name-only or schema-qualified format like DropStoredProcedureIfExists.
    /// </summary>
    public static MigrationBuilder DropFunctionIfExists(
        this MigrationBuilder migrationBuilder,
        string functionName,
        string schema = "dbo")
    {
        var (s, n) = NormalizeName(schema, functionName);

        var sql = $@"
IF OBJECT_ID(N'[{s}].[{n}]', 'FN') IS NOT NULL
    DROP FUNCTION [{s}].[{n}];
IF OBJECT_ID(N'[{s}].[{n}]', 'IF') IS NOT NULL
    DROP FUNCTION [{s}].[{n}];
IF OBJECT_ID(N'[{s}].[{n}]', 'TF') IS NOT NULL
    DROP FUNCTION [{s}].[{n}];";

        migrationBuilder.Sql(sql);
        return migrationBuilder;
    }

    /// <summary>
    /// Drops a view if it exists.
    /// Accepts either name-only or schema-qualified.
    /// </summary>
    public static MigrationBuilder DropViewIfExists(
        this MigrationBuilder migrationBuilder,
        string viewName,
        string schema = "dbo")
    {
        var (s, n) = NormalizeName(schema, viewName);

        var sql = $@"
IF OBJECT_ID(N'[{s}].[{n}]', 'V') IS NOT NULL
    DROP VIEW [{s}].[{n}];";

        migrationBuilder.Sql(sql);
        return migrationBuilder;
    }

    /// <summary>
    /// Normalizes object naming so callers can pass either "Name" or "schema.Name".
    /// This prevents mistakes like "dbo.dbo.Name".
    /// </summary>
    private static (string Schema, string Name) NormalizeName(string defaultSchema, string nameOrQualified)
    {
        var parts = nameOrQualified.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
            return (parts[0], parts[1]);

        return (defaultSchema, nameOrQualified);
    }
}
