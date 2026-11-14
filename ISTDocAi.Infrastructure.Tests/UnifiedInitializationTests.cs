using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISTDocAi.Application;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Startup;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.Startup.DataInitialization;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class UnifiedInitializationTests
{
    private static string Root
    {
        get {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "ISTDocAi.sln"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Repository unavailable");
        }
    }
    private static AppDbContext Model() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=invalid;Database=NeverConnect", s => s.MigrationsHistoryTable("__DocAiIamMigrationsHistory", "dbo")).Options);
    internal static HashSet<string> IamTables()
    {
        using var context = Model();
        return context.Model.GetRelationalModel().Tables.Select(t => t.Name).Append("__DocAiIamMigrationsHistory").ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public void Iam_model_and_generated_migration_have_no_DocAi_object_collisions()
    {
        using var context = Model();
        var model = context.Model.GetRelationalModel();
        var iamNames = model.Tables.Select(t => t.Name)
            .Concat(model.Tables.SelectMany(t => t.UniqueConstraints.Select(k => k.Name)))
            .Concat(model.Tables.SelectMany(t => t.ForeignKeyConstraints.Select(k => k.Name)))
            .Concat(model.Tables.SelectMany(t => t.Indexes.Select(k => k.Name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalog = SchemaCatalog.Baseline();
        var docNames = catalog.Structure.Select(row => JsonDocument.Parse(row)).SelectMany(d => new[] { "name", "table" }
            .Where(k => d.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
            .Select(k => d.RootElement.GetProperty(k).GetString()!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mapping = SqlBatchReader.ReadResource("ModelMappingProfiles.sql");
        foreach (Match match in Regex.Matches(mapping, @"(?i)(?:TABLE|PROCEDURE|FUNCTION|VIEW|TRIGGER|CONSTRAINT|INDEX|ROLE)\s+(?:dbo\.)?\[?(\w+)"))
            docNames.Add(match.Groups[1].Value);
        Assert.That(iamNames.Intersect(docNames, StringComparer.OrdinalIgnoreCase), Is.Empty);
        var sql = context.GetService<IMigrator>().GenerateScript();
        var migrationObjects = Regex.Matches(sql, @"(?i)(?:CREATE TABLE|CONSTRAINT|CREATE(?: UNIQUE)? INDEX)\s+\[([^\]]+)\](?:\.\[([^\]]+)\])?")
            .Select(m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value).ToArray();
        Assert.That(migrationObjects.Intersect(docNames, StringComparer.OrdinalIgnoreCase), Is.Empty);
        Assert.That(sql, Does.Contain("__DocAiIamMigrationsHistory").And.Not.Contain("[__EFMigrationsHistory]"));
        Assert.That(Regex.IsMatch(sql, @"(?i)CREATE\s+(ROLE|SCHEMA)\b"), Is.False);
        Assert.That(model.Tables.All(t => t.Schema is null or "dbo"), Is.True);
        TestContext.Out.WriteLine("IAM tables: " + string.Join(", ", IamTables().Order()));
        Assert.That(context.Database.GetMigrations().ToArray(), Has.Length.EqualTo(1));
    }

    [Test]
    public void Lock_adapter_preserves_the_frozen_DocAi_sequence_except_two_continuation_calls()
    {
        var frozen = File.ReadAllText(Path.Combine(Root, "ISTDocAi.Infrastructure/Startup/DatabaseMigration/DocAi/DatabaseInitializer.cs"));
        var adapter = File.ReadAllText(Path.Combine(Root, "ISTDocAi.Infrastructure/Startup/DatabaseMigration/DocAi/LockedDatabaseSequence.cs"));
        string Body(string s) => s[s.IndexOf("        if (options.CommandTimeoutSeconds", StringComparison.Ordinal)..s.IndexOf("    internal static async", StringComparison.Ordinal)];
        frozen = Body(frozen);
        adapter = adapter[adapter.IndexOf("        if (options.CommandTimeoutSeconds", StringComparison.Ordinal)..];
        adapter = adapter[..adapter.LastIndexOf('}')].TrimEnd() + "\n\n";
        adapter = adapter.Replace("DatabaseInitializer.ValidateHistoryAsync", "ValidateHistoryAsync")
            .Replace("                    await completeIamAsync(ct);\n", "")
            .Replace("            await completeIamAsync(ct);\n", "");
        adapter = Regex.Replace(adapter, @"await completeIamAsync\(ct\);", "");
        Assert.That(Regex.Replace(adapter, @"\s+", ""), Is.EqualTo(Regex.Replace(frozen, @"\s+", "")));
    }

    [TestCase(null, "docai", "docai")]
    [TestCase("", "docai", "docai")]
    [TestCase("template", "docai", "template")]
    public void Runtime_connection_precedence(string? template, string docai, string expected)
    {
        // Preserve the existing cases while proving that stale configuration cannot fall back.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"] = template, ["DocAi:Sql:ConnectionString"] = docai }).Build();
        Assert.That(() => DatabaseConfiguration.RuntimeConnection(config), Throws.InvalidOperationException.With.Message.EqualTo(
            "DocAi:Sql:ConnectionString was removed; set ConnectionStrings:DefaultConnection."));
        config["DocAi:Sql:ConnectionString"] = null;
        if (string.IsNullOrWhiteSpace(template))
            Assert.That(() => DatabaseConfiguration.RuntimeConnection(config), Throws.InvalidOperationException.With.Message.EqualTo(
                "ConnectionStrings:DefaultConnection is required."));
        else
            Assert.That(DatabaseConfiguration.RuntimeConnection(config), Is.EqualTo(expected));
    }

    private sealed class EventLogger : ILogger<UnifiedDatabaseInitializer>
    {
        public List<string> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.That(exception, Is.Null);
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                Events.Add(values.First(v => v.Key == "event").Value!.ToString()!);
        }
    }
    private static ServiceProvider Host(DisposableDatabase db, bool migrate, EventLogger? logger = null, IStartupTask? probe = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"] = db.ConnectionString,
            ["DocAi:Database:MigrateOnStartup"] = migrate.ToString(),
            ["DocAi:Database:InitializationTimeoutSeconds"] = "900",
            ["TemplateEndpoints:Enabled"] = "false" }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IConfiguration>(config);
        services.AddApplication(); services.AddInfrastructure(db.ConnectionString, false);
        services.AddScoped<IStartupTask, SeederExecutorTask>();
        services.AddDocAiDatabase(config, true);
        if (logger is not null) services.AddSingleton<ILogger<UnifiedDatabaseInitializer>>(logger);
        if (probe is not null) services.AddSingleton(probe);
        return services.BuildServiceProvider();
    }
    private static async Task<bool> Run(ServiceProvider host) =>
        await host.GetRequiredService<UnifiedDatabaseInitializer>().InitializeAsync();
    private static async Task Verify(DisposableDatabase db)
    {
        using var context = Model();
        context.Database.SetConnectionString(db.ConnectionString);
        Assert.That((await context.Database.GetAppliedMigrationsAsync()).ToArray(), Is.EqualTo(context.Database.GetMigrations().ToArray()));
        await using var connection = new SqlConnection(db.ConnectionString); await connection.OpenAsync();
        var rows = new List<object>();
        await using (var command = new SqlCommand("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId", connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) rows.Add(new { migrationId = reader.GetString(0), productVersion = reader.GetString(1) });
        Assert.That(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }) + "\n"),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/migration-history.json"))));
        var catalog = await SchemaCatalog.ReadAsync(connection, null, 300, CancellationToken.None, fullSecurity: true);
        var tables = IamTables();
        bool Keep(string row)
        {
            using var d = JsonDocument.Parse(row);
            foreach (var key in new[] { "table", "objectname" })
                if (d.RootElement.TryGetProperty(key, out var t) && t.ValueKind == JsonValueKind.String && tables.Contains(t.GetString()!)) return false;
            return !(d.RootElement.TryGetProperty("temporal_type", out _) && tables.Contains(d.RootElement.GetProperty("name").GetString()!));
        }
        var filtered = new SchemaCatalog(catalog.Structure.Where(Keep).ToArray(), catalog.Security.Where(Keep).ToArray());
        Assert.That(Encoding.UTF8.GetBytes(filtered.ToJson() + "\n"), Is.EqualTo(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/fresh-schema-security.json"))));
    }

    [Test, Category("Sql")]
    public async Task Fresh_then_restart_then_verify_only_preserve_histories_catalog_and_seed_counts()
    {
        SqlTestGuard.Require(); await using var db = new DisposableDatabase();
        var events = new EventLogger(); using var host = Host(db, true, events);
        Assert.That(host.GetRequiredService<IDatabaseReadiness>().Status.State, Is.EqualTo(DatabaseReadinessState.Pending));
        Assert.That(await Run(host), Is.True);
        Assert.That(host.GetRequiredService<IDatabaseReadiness>().Status.State, Is.EqualTo(DatabaseReadinessState.Ready));
        Assert.That(events.Events, Is.EqualTo(new[] { "database.initialization_started", "database.migration_started", "database.created", "database.migration_completed",
            "database.iam_migration_started", "database.iam_migration_completed", "database.seeding_started", "database.seeding_completed", "database.initialization_completed" }));
        await Verify(db);
        var before = await SeedCounts(db);
        using var restart = Host(db, true); Assert.That(await Run(restart), Is.True);
        Assert.That(await SeedCounts(db), Is.EqualTo(before));
        using var verifyOnly = Host(db, false); Assert.That(await Run(verifyOnly), Is.True);
        Assert.That(await SeedCounts(db), Is.EqualTo(before)); await Verify(db);
    }
    private static async Task<string[]> SeedCounts(DisposableDatabase db)
    {
        var counts = new List<string>();
        foreach (var table in IamTables().Order()) counts.Add(await db.ScalarTarget($"SELECT COUNT_BIG(*) FROM dbo.[{table}]", CancellationToken.None));
        return counts.ToArray();
    }

    [TestCase("initial")]
    [TestCase("current")]
    [TestCase("legacy")]
    [Category("Sql")]
    public async Task Upgrade_supported_DocAi_states(string state)
    {
        SqlTestGuard.Require(); await using var db = new DisposableDatabase();
        if (state == "legacy")
        {
            await db.Create(CancellationToken.None);
            await db.ExecuteBatches(SqlBatchReader.Split(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"OracleSchema.sql"))), CancellationToken.None);
            foreach (var f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Legacy"),"*.sql").Order())
                await db.ExecuteBatches(SqlBatchReader.Split(await File.ReadAllTextAsync(f)), CancellationToken.None);
        }
        else
        {
            await using var context = db.Context();
            await context.GetService<IMigrator>().MigrateAsync(state == "initial" ? LegacySchemaAdopter.MigrationId : null);
        }
        using var host = Host(db, true); Assert.That(await Run(host), Is.True); await Verify(db);
    }

    [TestCase("ForeignTable")]
    [TestCase("IAM_User")]
    [Category("Sql")]
    public async Task Untracked_legacy_with_foreign_or_IAM_table_is_refused_without_changes(string table)
    {
        SqlTestGuard.Require(); await using var db = new DisposableDatabase();
        await using (var context = db.Context()) await context.GetService<IMigrator>().MigrateAsync(LegacySchemaAdopter.MigrationId);
        await db.Execute($"DROP TABLE dbo.__EFMigrationsHistory; CREATE TABLE dbo.[{table}](Id int NOT NULL);", CancellationToken.None);
        var before = (await db.Catalog(CancellationToken.None)).ToJson();
        using var host = Host(db, true); Assert.That(await Run(host), Is.False);
        Assert.That(host.GetRequiredService<IDatabaseReadiness>().Status, Is.EqualTo(new DatabaseReadinessStatus(DatabaseReadinessState.Failed,"DATABASE_SCHEMA_UNRECOGNIZED")));
        Assert.That((await db.Catalog(CancellationToken.None)).ToJson(), Is.EqualTo(before));
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM sys.tables WHERE name IN ('__EFMigrationsHistory','__DocAiIamMigrationsHistory')", CancellationToken.None), Is.EqualTo("0"));
    }

    [Test, Category("Sql")]
    public async Task Verify_only_missing_IAM_fails_without_applying()
    {
        SqlTestGuard.Require(); await using var db = new DisposableDatabase();
        await new DatabaseInitializer().InitializeAsync(new(), db.ConnectionString, _ => {}, CancellationToken.None);
        var before = (await db.Catalog(CancellationToken.None)).ToJson();
        using var host = Host(db, false); Assert.That(await Run(host), Is.False);
        Assert.That(host.GetRequiredService<IDatabaseReadiness>().Status.Code, Is.EqualTo("DATABASE_IAM_NOT_MIGRATED"));
        Assert.That((await db.Catalog(CancellationToken.None)).ToJson(), Is.EqualTo(before));
    }

    [Test, Category("Sql")]
    public async Task Concurrent_initializers_hold_master_lock_through_seeding()
    {
        SqlTestGuard.Require(); await using var db = new DisposableDatabase();
        var probe = new LockProbe(db.ConnectionString);
        using var first = Host(db, true, probe: probe); using var second = Host(db, true);
        var firstRun = Run(first);
        await probe.Entered.Task.WaitAsync(TimeSpan.FromMinutes(10));
        var secondRun = Run(second);
        try
        {
            await Task.Delay(200);
            Assert.That(secondRun.IsCompleted, Is.False);
        }
        finally { probe.Release.TrySetResult(); }
        Assert.That(await Task.WhenAll(firstRun, secondRun), Is.All.True);
        await Verify(db);
    }
    private sealed class LockProbe(string connection) : IStartupTask
    {
        public StartupStage Stage => StartupStage.SystemInitialization;
        public int Order => 0;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ExecuteAsync(IServiceProvider services, CancellationToken ct)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connection);
                var name = builder.InitialCatalog; builder.InitialCatalog = "master"; builder.Pooling = false;
                await using var sql = new SqlConnection(builder.ConnectionString); await sql.OpenAsync(ct);
                await using var cmd = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r",sql);
                cmd.Parameters.AddWithValue("@resource", "DocAi.Database.Initialize:" + name);
                Assert.That(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)), Is.LessThan(0));
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            catch (Exception e) { Entered.TrySetException(e); throw; }
        }
    }
}
