using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

/// <summary>Schema-only startup. Never retries and never opens runtime Dapper connections.</summary>
public sealed record DatabaseInitializationEvent(string Event, string? Code);

public sealed class DatabaseInitializer
{
    public async Task InitializeAsync(DatabaseOptions options, string connectionString,
        Action<DatabaseInitializationEvent> report, CancellationToken stoppingToken)
    {
        if (options.CommandTimeoutSeconds <= 0 || options.InitializationTimeoutSeconds <= 0)
            throw new DatabaseInitializationException("DATABASE_CONFIGURATION_INVALID");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.InitializationTimeoutSeconds));
        var ct = timeout.Token;
        var started = Stopwatch.StartNew();
        void Report(string name, string? code = null) => report(new(name, code));
        try
        {
            Report("database.initialization_started");
            var target = new SqlConnectionStringBuilder(connectionString) { ConnectRetryCount = 0 };
            if (string.IsNullOrWhiteSpace(target.InitialCatalog) || !string.IsNullOrEmpty(target.AttachDBFilename))
                throw new DatabaseInitializationException("DATABASE_CONFIGURATION_INVALID");
            var name = target.InitialCatalog;
            target.ConnectTimeout = Math.Min(Math.Max(1, target.ConnectTimeout), options.InitializationTimeoutSeconds);
            var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master", Pooling = false };
            // Non-pooled master session: disposing always releases the session lock, including failure paths.
            await using var lockConnection = new SqlConnection(master.ConnectionString);
            await lockConnection.OpenAsync(ct);
            await using (var command = new SqlCommand("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive',
                    @LockOwner='Session', @LockTimeout=@timeout, @DbPrincipal='public';
                SELECT @result;
                """, lockConnection) { CommandTimeout = Math.Max(1, (int)Math.Ceiling(options.InitializationTimeoutSeconds - started.Elapsed.TotalSeconds)) })
            {
                command.Parameters.AddWithValue("@resource", "DocAi.Database.Initialize:" + name);
                command.Parameters.AddWithValue("@timeout", Math.Max(0, (int)Math.Min(int.MaxValue, (options.InitializationTimeoutSeconds - started.Elapsed.TotalSeconds) * 1000)));
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) < 0)
                    throw new DatabaseInitializationException("DATABASE_INITIALIZATION_TIMEOUT");
            }
            bool exists;
            await using (var command = new SqlCommand("SELECT DB_ID(@name)", lockConnection) { CommandTimeout = options.CommandTimeoutSeconds })
            {
                command.Parameters.AddWithValue("@name", name);
                exists = await command.ExecuteScalarAsync(ct) is not DBNull;
            }
            if (!exists && !options.MigrateOnStartup) throw new DatabaseInitializationException("DATABASE_NOT_MIGRATED");
            if (!exists && !options.AllowDatabaseCreation) throw new DatabaseInitializationException("DATABASE_MISSING");
            await using var context = new DocAiDbContext(new DbContextOptionsBuilder<DocAiDbContext>()
                .UseSqlServer(target.ConnectionString, sql => sql.CommandTimeout(options.CommandTimeoutSeconds))
                .Options);
            if (exists)
            {
                await using var connection = new SqlConnection(target.ConnectionString);
                await connection.OpenAsync(ct);
                var tracked = await ValidateHistoryAsync(connection, context.Database.GetMigrations().ToArray(), options.CommandTimeoutSeconds, ct);
                var pending = (await context.Database.GetPendingMigrationsAsync(ct)).Any();
                if (!options.MigrateOnStartup)
                {
                    if (!tracked || pending) throw new DatabaseInitializationException("DATABASE_NOT_MIGRATED");
                    return;
                }
                if (!tracked)
                {
                    await using var probe = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped=0 AND name<>'__EFMigrationsHistory' AND type IN ('U','V','P','FN','IF','TF','TR','SO')", connection) { CommandTimeout = options.CommandTimeoutSeconds };
                    if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) > 0)
                    {
                        Report("database.adoption_started");
                        try { await new LegacySchemaAdopter().AdoptAsync(connection, options.CommandTimeoutSeconds, ct); }
                        catch (Exception error)
                        {
                            Report("database.adoption_refused", error is DatabaseInitializationException known
                                ? known.Code : ct.IsCancellationRequested ? "DATABASE_INITIALIZATION_TIMEOUT" : "DATABASE_INITIALIZATION_FAILED");
                            throw;
                        }
                        Report("database.adoption_completed");
                    }
                }
            }
            Report("database.migration_started");
            await context.Database.MigrateAsync(ct);
            if (!exists) Report("database.created");
            if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new DatabaseInitializationException("DATABASE_NOT_MIGRATED");
            Report("database.migration_completed");
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        { throw new DatabaseInitializationException("DATABASE_INITIALIZATION_TIMEOUT"); }
        catch (SqlException error) when (error.Number == -2)
        { throw new DatabaseInitializationException("DATABASE_INITIALIZATION_TIMEOUT"); }
    }

    internal static async Task<bool> ValidateHistoryAsync(SqlConnection connection, string[] migrations, int commandTimeout, CancellationToken ct, SqlTransaction? transaction = null)
    {
        await using var command = new SqlCommand("SELECT OBJECT_ID(N'dbo.__EFMigrationsHistory')", connection, transaction) { CommandTimeout = commandTimeout };
        if (await command.ExecuteScalarAsync(ct) is DBNull) return false;
        // Reject malformed history before EF can interpret or write it.
        command.CommandText = """
            SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.__EFMigrationsHistory'))=2
              AND EXISTS (SELECT 1 FROM sys.key_constraints k JOIN sys.index_columns ic
                ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
                WHERE k.parent_object_id=OBJECT_ID(N'dbo.__EFMigrationsHistory') AND k.type='PK'
                  AND COL_NAME(ic.object_id,ic.column_id)='MigrationId' AND ic.key_ordinal=1)
              AND (SELECT COUNT(*) FROM sys.key_constraints k JOIN sys.index_columns ic
                ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
                WHERE k.parent_object_id=OBJECT_ID(N'dbo.__EFMigrationsHistory') AND k.type='PK' AND ic.key_ordinal>0)=1
              AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.__EFMigrationsHistory')
                AND (is_identity=1 OR is_computed=1 OR default_object_id<>0))
              AND NOT EXISTS (SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.__EFMigrationsHistory'))
              THEN (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.__EFMigrationsHistory') AND
              ((name='MigrationId' AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=300 AND is_nullable=0) OR
               (name='ProductVersion' AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=64 AND is_nullable=0))) ELSE 0 END;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 2)
            throw new DatabaseInitializationException("DATABASE_HISTORY_UNRECOGNIZED");
        command.CommandText = "SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId";
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<(string MigrationId, string ProductVersion)>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }
        return ValidateHistoryRows(rows, migrations);
    }

    internal static bool ValidateHistoryRows(IEnumerable<(string MigrationId, string ProductVersion)> rows, string[] migrations)
    {
        var count = 0;
        foreach (var row in rows)
        {
            if (count >= migrations.Length || row.MigrationId != migrations[count] || string.IsNullOrWhiteSpace(row.ProductVersion))
                throw new DatabaseInitializationException("DATABASE_HISTORY_UNRECOGNIZED");
            count++;
        }
        return count > 0;
    }
}
