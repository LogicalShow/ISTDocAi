using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

/// <summary>Schema-only startup. Never retries and never opens runtime Dapper connections.</summary>


internal sealed class LockedDatabaseSequence
{
    public async Task InitializeAsync(DatabaseOptions options, string connectionString,
        Action<DatabaseInitializationEvent> report, CancellationToken stoppingToken,
        Func<CancellationToken, Task> completeIamAsync)
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
                var tracked = await DatabaseInitializer.ValidateHistoryAsync(connection, context.Database.GetMigrations().ToArray(), options.CommandTimeoutSeconds, ct);
                var pending = (await context.Database.GetPendingMigrationsAsync(ct)).Any();
                if (!options.MigrateOnStartup)
                {
                    if (!tracked || pending) throw new DatabaseInitializationException("DATABASE_NOT_MIGRATED");
                    await completeIamAsync(ct);
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
            await completeIamAsync(ct);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        { throw new DatabaseInitializationException("DATABASE_INITIALIZATION_TIMEOUT"); }
        catch (SqlException error) when (error.Number == -2)
        { throw new DatabaseInitializationException("DATABASE_INITIALIZATION_TIMEOUT"); }
    }

}
