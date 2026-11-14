using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class AdoptionTests
{
    private static CancellationTokenSource Timeout() => new(TimeSpan.FromMinutes(15));
    private static Task Initialize(DisposableDatabase db, CancellationToken ct, DatabaseOptions? options = null) =>
        new DatabaseInitializer().InitializeAsync(options ?? new(), db.ConnectionString, _ => { }, ct);

    [Test]
    public void Embedded_catalog_parses() => Assert.That(SchemaCatalog.Baseline(), Is.Not.Null);

    [Test, Category("Sql")]
    public async Task Fresh_migration_regenerates_the_embedded_catalog_exactly()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout();
        await using var db = new DisposableDatabase();
        await using (var context = db.Context()) await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", timeout.Token);
        var actual = await db.Catalog(timeout.Token);
        var expected = SchemaCatalog.Baseline();
        Assert.That(actual.Structure, Is.EqualTo(expected.Structure));
        Assert.That(actual.Security, Is.EqualTo(expected.Security));
    }

    // Explicit opt-in export for the offline implementer: Claude runs this on disposable SQL.
    // Output is catalog metadata only; the database is always dropped by the fixture.
    [Test, Category("Sql")]
    public async Task Export_fresh_catalog_when_requested()
    {
        SqlTestGuard.Require();
        var output = Environment.GetEnvironmentVariable("DOCAI_TEST_CATALOG_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        using var timeout = Timeout();
        await using var db = new DisposableDatabase();
        await using (var context = db.Context()) await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", timeout.Token);
        await File.WriteAllTextAsync(output, (await db.Catalog(timeout.Token)).ToJson(), timeout.Token);
    }

    private static async Task Legacy(DisposableDatabase db, CancellationToken ct)
    {
        await db.Create(ct);
        await db.ExecuteBatches(SqlBatchReader.Split(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OracleSchema.sql"), ct)), ct);
        var upgrades = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Legacy"), "*.sql").Order(StringComparer.Ordinal);
        Assert.That(upgrades.Count(), Is.EqualTo(6));
        foreach (var file in upgrades)
            await db.ExecuteBatches(SqlBatchReader.Split(await File.ReadAllTextAsync(file, ct)), ct);
    }

    [Test, Category("Sql")]
    public async Task DocAiLocal_profile_adopts_preserves_business_rows_and_is_repeatable()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        await db.Execute(Seed, ct);
        var tables = new[] { "Consumers", "Jobs", "JobRuns", "AzureOperations", "UsageLedger", "PageAllowanceGrants" };
        var before = new List<string>();
        foreach (var table in tables) before.Add(await db.ScalarTarget($"SELECT * FROM dbo.{table} FOR JSON PATH,INCLUDE_NULL_VALUES", ct));
        await Initialize(db, ct);
        await db.VerifyHistoryAndRcsi(ct);
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM sys.tables WHERE name IN ('MappingProfiles','MappingFields','ModelMappingBindings')", ct), Is.EqualTo("3"));
        for (var i = 0; i < tables.Length; i++)
            Assert.That(await db.ScalarTarget($"SELECT * FROM dbo.{tables[i]} FOR JSON PATH,INCLUDE_NULL_VALUES", ct), Is.EqualTo(before[i]));
        var snapshot = await db.Snapshot(ct);
        await Initialize(db, ct);
        Assert.That(await db.Snapshot(ct), Is.EqualTo(snapshot));
        await db.VerifyHistoryAndRcsi(ct);
    }

    [Test, Category("Sql")]
    public async Task Structural_difference_refuses_without_security_or_history_changes()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        await db.Execute("DROP INDEX IX_Jobs_Queue ON dbo.Jobs", ct);
        var before = await db.Catalog(ct);
        var error = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct));
        Assert.That(error!.Code, Is.EqualTo("DATABASE_SCHEMA_UNRECOGNIZED"));
        var after = await db.Catalog(ct);
        Assert.That(after.Structure, Is.EqualTo(before.Structure));
        Assert.That(after.Security, Is.EqualTo(before.Security));
        Assert.That(await db.ScalarTarget("SELECT OBJECT_ID(N'dbo.__EFMigrationsHistory')", ct), Is.EqualTo(""));
    }

    [Test, Category("Sql")]
    public async Task Rcsi_off_refuses_without_writes()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        await db.Execute($"ALTER DATABASE [{db.Name}] SET READ_COMMITTED_SNAPSHOT OFF", ct);
        var before = await db.Catalog(ct);
        var error = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct));
        Assert.That(error!.Code, Is.EqualTo("DATABASE_RCSI_OFF"));
        Assert.That((await db.Catalog(ct)).Security, Is.EqualTo(before.Security));
        Assert.That(await db.ScalarTarget("SELECT OBJECT_ID(N'dbo.__EFMigrationsHistory')", ct), Is.EqualTo(""));
    }

    [Test, Category("Sql")]
    public async Task Missing_database_creation_policy_and_concurrent_initializers()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        var error = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct, new() { AllowDatabaseCreation = false }));
        Assert.That(error!.Code, Is.EqualTo("DATABASE_MISSING"));
        Assert.That(await db.ScalarMaster($"SELECT COUNT(*) FROM sys.databases WHERE name=N'{db.Name}'", ct), Is.EqualTo("0"));
        await Task.WhenAll(Initialize(db, ct), Initialize(db, ct));
        await db.VerifyHistoryAndRcsi(ct);
    }

    [Test, Category("Sql")]
    public async Task Disabled_migration_is_read_only_and_accepts_only_migrated_database()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        var options = new DatabaseOptions { MigrateOnStartup = false };
        var missing = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct, options));
        Assert.That(missing.Code, Is.EqualTo("DATABASE_NOT_MIGRATED"));
        await db.Create(ct);
        var before = await db.Catalog(ct);
        var empty = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct, options));
        Assert.That(empty.Code, Is.EqualTo("DATABASE_NOT_MIGRATED"));
        var after = await db.Catalog(ct);
        Assert.That(after.Structure, Is.EqualTo(before.Structure));
        Assert.That(after.Security, Is.EqualTo(before.Security));
        await Initialize(db, ct);
        await Initialize(db, ct, options);
        await db.VerifyHistoryAndRcsi(ct);
    }

    [Test, Category("Sql")]
    public async Task Extra_permission_refuses_and_rolls_back_new_roles_and_grants()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        await db.Execute("GRANT DELETE ON dbo.Consumers TO docai_quota_operator", ct);
        var before = await db.Catalog(ct);
        var error = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct));
        Assert.That(error!.Code, Is.EqualTo("DATABASE_SCHEMA_UNRECOGNIZED"));
        Assert.That((await db.Catalog(ct)).Security, Is.EqualTo(before.Security));
        Assert.That(await db.ScalarTarget("SELECT OBJECT_ID(N'dbo.__EFMigrationsHistory')", ct), Is.EqualTo(""));
    }

    [Test, Category("Sql")]
    public async Task Empty_history_is_adopted_but_unknown_rows_and_malformed_history_are_refused()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        await db.Execute("CREATE TABLE dbo.__EFMigrationsHistory(MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL)", ct);
        await Initialize(db, ct);
        await db.VerifyHistoryAndRcsi(ct);
        await db.Execute("INSERT dbo.__EFMigrationsHistory VALUES ('99999999999999_Unknown','10.0.13')", ct);
        var before = await db.ScalarTarget("SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH", ct);
        var unknown = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct));
        Assert.That(unknown.Code, Is.EqualTo("DATABASE_HISTORY_UNRECOGNIZED"));
        Assert.That(await db.ScalarTarget("SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH", ct), Is.EqualTo(before));
        await db.Execute("DROP TABLE dbo.__EFMigrationsHistory; CREATE TABLE dbo.__EFMigrationsHistory(Wrong int)", ct);
        var malformed = Assert.ThrowsAsync<DatabaseInitializationException>(() => Initialize(db, ct));
        Assert.That(malformed.Code, Is.EqualTo("DATABASE_HISTORY_UNRECOGNIZED"));
    }

    [Test, Category("Sql")]
    public async Task EnvironmentalPermissionsAreIgnoredButDocAiPermissionsRemainExact()
    {
        SqlTestGuard.Require();
        using var timeout = Timeout(); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Legacy(db, ct);
        var before = await db.Catalog(ct);
        await db.Execute("CREATE USER environmentUser WITHOUT LOGIN; GRANT CONNECT TO environmentUser; CREATE ROLE environmentRole; GRANT SELECT ON dbo.Consumers TO environmentRole;", ct);
        Assert.That((await db.Catalog(ct)).Security, Is.EqualTo(before.Security));
        await Initialize(db, ct);
        await db.VerifyHistoryAndRcsi(ct);
        var adopted = await db.Catalog(ct);
        await db.Execute("GRANT DELETE ON dbo.Consumers TO docai_modelteam;", ct);
        Assert.That((await db.Catalog(ct)).Security, Is.Not.EqualTo(adopted.Security));
    }

    private const string Seed = """
        INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1)
          VALUES ('ADOPT',N'Adoption test','FWD','ADOPT',HASHBYTES('SHA2_256','offline-fixture'));
        DECLARE @consumer smallint = SCOPE_IDENTITY(), @job uniqueidentifier = NEWID();
        INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,ShippingLineCode,Version,Status)
          VALUES ('adoption-model','extract','BL',NULL,1,'Active');
        INSERT dbo.Jobs(JobId,ConsumerId,DocumentTypeCode,IdemLine,FileSha256,ContentType,FileSizeBytes,PageCount,BlobPath,Stage,CorrelationId,Status)
          VALUES (@job,@consumer,'BL','*',HASHBYTES('SHA2_256','document'),'application/pdf',100,1,'fixture','Extract','adoption','Completed');
        INSERT dbo.JobRuns(JobId,RunNo,Reason,ConsumerId,PageCount,CorrelationId,Status)
          VALUES (@job,1,'New',@consumer,1,'adoption','Completed');
        INSERT dbo.AzureOperations(JobId,RunNo,Kind,ModelId,State,OperationLocation,Pages)
          VALUES (@job,1,'extract','adoption-model','Succeeded','fixture',1);
        DECLARE @operation bigint = SCOPE_IDENTITY();
        INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,CorrelationId)
          VALUES (@consumer,@job,1,@operation,'DI_CUSTOM_EXTRACT','adoption-model',1,30,SYSUTCDATETIME(),'adoption');
        INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId)
          VALUES (@consumer,100,'TopUp',N'fixture',N'Preservation test',NEWID());
        """;
}
