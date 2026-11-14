using System.Diagnostics;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Startup;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.BackgroundServices.DocAi;

public static class DatabaseRegistration
{
    public static IServiceCollection AddDocAiDatabase(this IServiceCollection services, IConfiguration configuration, bool seedMode)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(o => {
                if (string.IsNullOrWhiteSpace(o.MigrationConnectionString))
                    o.MigrationConnectionString = DatabaseConfiguration.RuntimeConnection(configuration);
            });
        services.TryAddSingleton<DatabaseReadiness>();
        services.TryAddSingleton<IDatabaseReadiness>(s => s.GetRequiredService<DatabaseReadiness>());
        services.AddSingleton<UnifiedDatabaseInitializer>();
        if (!seedMode) services.AddHostedService<DatabaseInitializationService>();
        return services;
    }
}

public static class DatabaseConfiguration
{
    public static string RuntimeConnection(IConfiguration configuration)
    {
        RejectRemovedConnection(configuration);
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        return connection;
    }

    public static void RejectRemovedConnection(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["DocAi:Sql:ConnectionString"]))
            throw new InvalidOperationException("DocAi:Sql:ConnectionString was removed; set ConnectionStrings:DefaultConnection.");
    }
}

public sealed class UnifiedDatabaseInitializer(IOptions<DatabaseOptions> options, DatabaseReadiness gate,
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<UnifiedDatabaseInitializer> logger)
{
    public async Task<bool> InitializeAsync(CancellationToken stoppingToken = default)
    {
        var started = Stopwatch.StartNew();
        var timeoutSeconds = 900;
        void Report(DatabaseInitializationEvent item) => logger.LogInformation(
            "{event} errorCode={errorCode} durationMs={durationMs} migrationId={migrationId}",
            item.Event, item.Code, started.Elapsed.TotalMilliseconds, LegacySchemaAdopter.MigrationId);
        try
        {
            var settings = options.Value;
            timeoutSeconds = settings.InitializationTimeoutSeconds;
            if (string.IsNullOrWhiteSpace(settings.MigrationConnectionString))
                throw new DatabaseInitializationException("DATABASE_CONFIGURATION_INVALID");
            // Migration credentials may differ; the server/database must be identical.
            var migration = new SqlConnectionStringBuilder(settings.MigrationConnectionString) { ConnectRetryCount = 0 };
            migration.ConnectTimeout = Math.Min(Math.Max(1, migration.ConnectTimeout), Math.Max(1, timeoutSeconds));
            var runtime = new SqlConnectionStringBuilder(DatabaseConfiguration.RuntimeConnection(configuration));
            if (!string.Equals(migration.DataSource, runtime.DataSource, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(migration.InitialCatalog, runtime.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new DatabaseInitializationException("DATABASE_CONFIGURATION_INVALID");
            await new LockedDatabaseSequence().InitializeAsync(settings, settings.MigrationConnectionString, Report,
                stoppingToken, async ct => {
                    using var scope = scopes.CreateScope();
                    scope.ServiceProvider.GetRequiredService<ISTDocAi.Infrastructure.Data.Interceptors.StartupCancellationInterceptor>().Token = ct;
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    context.Database.SetConnectionString(migration.ConnectionString);
                    context.Database.SetCommandTimeout(settings.CommandTimeoutSeconds);
                    Report(new("database.iam_migration_started", null));
                    if (settings.MigrateOnStartup) await context.Database.MigrateAsync(ct);
                    if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
                        throw new DatabaseInitializationException("DATABASE_IAM_NOT_MIGRATED");
                    Report(new("database.iam_migration_completed", null));
                    Report(new("database.seeding_started", null));
                    // Startup tasks share this scoped context and migration credentials. Do not run the
                    // template migration task, which the two explicit chains above replace.
                    foreach (var task in scope.ServiceProvider.GetServices<IStartupTask>()
                        .Where(t => t is not DatabaseMigrationTask).DistinctBy(t => t.GetType())
                        .OrderBy(t => t.Stage).ThenBy(t => t.Order))
                    {
                        ct.ThrowIfCancellationRequested();
                        await task.ExecuteAsync(scope.ServiceProvider, ct);
                    }
                    ct.ThrowIfCancellationRequested();
                    Report(new("database.seeding_completed", null));
                });
            gate.MarkReady();
            Report(new("database.initialization_completed", null));
            return true;
        }
        catch (Exception error)
        {
            var code = error is DatabaseInitializationException known ? known.Code
                : stoppingToken.IsCancellationRequested ? "DATABASE_INITIALIZATION_CANCELLED"
                : error is OperationCanceledException || started.Elapsed.TotalSeconds >= timeoutSeconds ? "DATABASE_INITIALIZATION_TIMEOUT" : "DATABASE_INITIALIZATION_FAILED";
            gate.MarkFailed(code);
            logger.LogError("{event} errorCode={errorCode} durationMs={durationMs} migrationId={migrationId}",
                "database.initialization_failed", code, started.Elapsed.TotalMilliseconds, LegacySchemaAdopter.MigrationId);
            return false;
        }
    }
}

public sealed class DatabaseInitializationService(UnifiedDatabaseInitializer initializer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await initializer.InitializeAsync(stoppingToken);
    }
}
