using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

public sealed class LegacySchemaAdopter
{
    public const string MigrationId = "20261101000000_InitialSchema";
    public const string ProductVersion = "10.0.13";

    // Caller holds the session-owned initialization lock on master throughout this operation.
    public async Task AdoptAsync(SqlConnection connection, int commandTimeout, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        if (await DatabaseInitializer.ValidateHistoryAsync(connection, [MigrationId], commandTimeout, ct, transaction))
            throw new DatabaseInitializationException("DATABASE_HISTORY_UNRECOGNIZED");
        var baseline = SchemaCatalog.Baseline();
        var actual = await SchemaCatalog.ReadAsync(connection, transaction, commandTimeout, ct);
        if (baseline.Structure.Length == 0 || !actual.Structure.SequenceEqual(baseline.Structure))
            throw new DatabaseInitializationException("DATABASE_SCHEMA_UNRECOGNIZED");
        await using (var rcsi = new SqlCommand("SELECT CONVERT(int,is_read_committed_snapshot_on) FROM sys.databases WHERE name=DB_NAME()", connection, transaction) { CommandTimeout = commandTimeout })
            if (Convert.ToInt32(await rcsi.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new DatabaseInitializationException("DATABASE_RCSI_OFF");
        foreach (var batch in SqlBatchReader.Split(SqlBatchReader.ReadResource("InitialRoles.sql")))
        {
            var safe = Regex.Replace(batch, @"\bCREATE ROLE (docai_[a-z_]+);", m =>
                $"IF DATABASE_PRINCIPAL_ID(N'{m.Groups[1].Value}') IS NULL CREATE ROLE {m.Groups[1].Value} AUTHORIZATION dbo;",
                RegexOptions.IgnoreCase);
            // Catalog comparison includes grantors: make ownership/grants independent of the startup user's name.
            safe = Regex.Replace(safe, @"\bTO\s+(docai_[a-z_]+)\s*;", "TO $1 AS dbo;", RegexOptions.IgnoreCase);
            await using var command = new SqlCommand(safe, connection, transaction) { CommandTimeout = commandTimeout };
            await command.ExecuteNonQueryAsync(ct);
        }
        actual = await SchemaCatalog.ReadAsync(connection, transaction, commandTimeout, ct);
        if (!actual.Security.SequenceEqual(baseline.Security))
            throw new DatabaseInitializationException("DATABASE_SCHEMA_UNRECOGNIZED");
        await using (var stamp = new SqlCommand("""
            IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
              CREATE TABLE dbo.__EFMigrationsHistory (
                MigrationId nvarchar(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                ProductVersion nvarchar(32) NOT NULL);
            INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES (@id,@version);
            """, connection, transaction) { CommandTimeout = commandTimeout })
        {
            stamp.Parameters.AddWithValue("@id", MigrationId);
            stamp.Parameters.AddWithValue("@version", ProductVersion);
            await stamp.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
