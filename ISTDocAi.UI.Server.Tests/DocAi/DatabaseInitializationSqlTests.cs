using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Infrastructure;

using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using ISTDocAi.Infrastructure.Tests.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class DatabaseInitializationSqlTests : GuardedDeploymentTests
{
    private static DeploymentInitializationHost Service(DatabaseOptions options, DatabaseReadiness gate) =>
        new(options, gate);

    [Test, Category("Sql")]
    public async Task Concurrent_hosted_initializers_create_missing_database_and_both_become_ready()
    {
        await using var db = new DisposableDatabase();
        var first = new DatabaseReadiness(); var second = new DatabaseReadiness();
        var options = new DatabaseOptions { MigrationConnectionString = db.ConnectionString };
        using var one = Service(options, first); using var two = Service(options, second);
        await Task.WhenAll(one.StartAsync(default), two.StartAsync(default));
        await Task.WhenAll(one.ExecuteTask!, two.ExecuteTask!).WaitAsync(TimeSpan.FromMinutes(16));
        Assert.Equal(DatabaseReadinessState.Ready, first.Status.State);
        Assert.Equal(DatabaseReadinessState.Ready, second.Status.State);
        await db.VerifyHistoryAndRcsi(default);
    }

    [Test, Category("Sql")]
    public async Task Missing_database_with_creation_disabled_fails_without_creating_it()
    {
        await using var db = new DisposableDatabase();
        var gate = new DatabaseReadiness();
        using var service = Service(new() { MigrationConnectionString = db.ConnectionString, AllowDatabaseCreation = false }, gate);
        await service.StartAsync(default); await service.ExecuteTask!;
        Assert.Equal(new DatabaseReadinessStatus(DatabaseReadinessState.Failed, "DATABASE_MISSING"), gate.Status);
        Assert.Equal("0", await db.ScalarMaster($"SELECT COUNT(*) FROM sys.databases WHERE name=N'{db.Name}'", default));
    }

    [Test, Category("Sql")]
    public async Task Read_only_mode_fails_empty_and_becomes_ready_after_explicit_migration()
    {
        await using var db = new DisposableDatabase();
        await db.Create(default);
        var options = new DatabaseOptions { MigrationConnectionString = db.ConnectionString, MigrateOnStartup = false };
        var failed = new DatabaseReadiness();
        using (var service = Service(options, failed))
        {
            await service.StartAsync(default); await service.ExecuteTask!;
            Assert.Equal(new DatabaseReadinessStatus(DatabaseReadinessState.Failed, "DATABASE_NOT_MIGRATED"), failed.Status);
        }
        Assert.Equal("", await db.ScalarTarget("SELECT OBJECT_ID(N'dbo.__EFMigrationsHistory')", default));
        using (var migration = Service(new() { MigrationConnectionString=db.ConnectionString }, new DatabaseReadiness())) { await migration.StartAsync(default); await migration.ExecuteTask!; }
        var ready = new DatabaseReadiness();
        using (var service = Service(options, ready))
        {
            await service.StartAsync(default); await service.ExecuteTask!;
            Assert.Equal(DatabaseReadinessState.Ready, ready.Status.State);
        }
    }
}
