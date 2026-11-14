using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class GoldenParityTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static void MatchesGolden(string name, object value)
    {
        var actual = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Assert.That(actual, Is.EqualTo(File.ReadAllBytes(Fixture(name))), name);
    }

    [Test, Category("Sql")]
    public async Task Fresh_initializer_matches_schema_history_and_event_golden_bytes()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        var events = new List<DatabaseInitializationEvent>();
        await new DatabaseInitializer().InitializeAsync(new(), db.ConnectionString, events.Add, ct);
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        var catalog = await SchemaCatalog.ReadAsync(connection, null, 300, ct, fullSecurity: true);
        var rows = new List<object>();
        await using (var command = new SqlCommand("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId", connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(new { migrationId = reader.GetString(0), productVersion = reader.GetString(1) });
        Assert.Multiple(() =>
        {
            MatchesGolden("fresh-schema-security.json", catalog);
            MatchesGolden("migration-history.json", rows);
            MatchesGolden("initializer-events.json", events);
        });
    }

    [Test]
    public void Copied_database_scripts_and_configuration_fixtures_are_byte_identical()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Fixture("copied-file-hashes.json")));
        foreach (var row in manifest.RootElement.EnumerateArray())
        {
            var path = row.GetProperty("file").GetString()!;
            Assert.That(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root!.FullName, path)))),
                Is.EqualTo(row.GetProperty("sha256").GetString()), path);
        }
    }

    [Test]
    public void Embedded_resources_keep_frozen_bytes_and_logical_names()
    {
        using var golden = JsonDocument.Parse(File.ReadAllBytes(Fixture("embedded-resources.json")));
        var expected = golden.RootElement.EnumerateArray().Where(r => r.GetProperty("assembly").GetString() == "DocAi.Database").ToArray();
        var assembly = typeof(DocAiDbContext).Assembly;
        Assert.That(assembly.GetManifestResourceNames().Where(n => n.StartsWith("DocAi.Database.Migrations.Sql.", StringComparison.Ordinal)),
            Is.EquivalentTo(expected.Select(r => r.GetProperty("name").GetString())));
        foreach (var row in expected)
        {
            using var stream = assembly.GetManifestResourceStream(row.GetProperty("name").GetString()!)!;
            Assert.That(Convert.ToHexStringLower(SHA256.HashData(stream)), Is.EqualTo(row.GetProperty("sha256").GetString()));
        }
    }

    [Test]
    public void Both_contexts_have_separate_migration_chains_without_connecting()
    {
        using var docai = new DocAiDesignTimeFactory().CreateDbContext([]);
        Assert.That(docai.Database.GetMigrations(), Is.EqualTo(new[] { "20261101000000_InitialSchema", "20261103000000_ModelMappingProfiles" }));
        using var template = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1;Database=OfflineOnly;Integrated Security=true").Options);
        Assert.That(template.Database.GetMigrations(), Is.EqualTo(new[] { "20261011062423_InitialCreate" }));
        Assert.That(docai.GetService<IHistoryRepository>().GetCreateScript(), Does.Contain("[__EFMigrationsHistory]"));
    }

    [Test]
    public void Every_database_source_matches_baseline_after_only_declared_namespace_changes()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        using var mappings = JsonDocument.Parse(File.ReadAllBytes(Fixture("source-map.json")));
        // Stage 5d replaces only the generated snapshot baseline; no migration/history row is added.
        // All other frozen hash expectations remain unchanged.
        using var golden = JsonDocument.Parse(File.ReadAllBytes(Fixture("migration-files.json")));
        var hashes = golden.RootElement.EnumerateArray().ToDictionary(r => r.GetProperty("file").GetString()!, r => r.GetProperty("sha256").GetString());
        foreach (var row in mappings.RootElement.EnumerateArray())
        {
            var bytes = File.ReadAllBytes(Path.Combine(root!.FullName, row.GetProperty("target").GetString()!));
            if (row.GetProperty("csharp").GetBoolean())
            {
                var text = Encoding.UTF8.GetString(bytes);
                // Approved single-key decision 2026-11-08: only this XML comment changes.
                if (row.GetProperty("source").GetString() == "src/DocAi.Database/DatabaseOptions.cs")
                    text = text.Replace("Null/empty means use ConnectionStrings:DefaultConnection during startup.",
                        "Null/empty means use DocAi:Sql:ConnectionString during startup.", StringComparison.Ordinal);
                // Stage 5d adds schema metadata in a separate partial; preserve the frozen context-shell hash.
                if (row.GetProperty("source").GetString() == "src/DocAi.Database/DocAiDbContext.cs")
                    text = text.Replace("sealed partial class DocAiDbContext", "sealed class DocAiDbContext", StringComparison.Ordinal);
                if (row.GetProperty("addedUsings").GetBoolean())
                    text = text.Replace("using ISTDocAi.Infrastructure.Data.DocAi;\r\nusing ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;\r\n", "", StringComparison.Ordinal);
                text = text.Replace("namespace ISTDocAi.Infrastructure.Migrations.DocAi", "namespace DocAi.Database.Migrations", StringComparison.Ordinal)
                    .Replace("namespace ISTDocAi.Infrastructure.Data.DocAi;", "namespace DocAi.Database;", StringComparison.Ordinal)
                    .Replace("namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;", "namespace DocAi.Database;", StringComparison.Ordinal)
                    .Replace("using ISTDocAi.Infrastructure.Data.DocAi;", "using DocAi.Database;", StringComparison.Ordinal);
                bytes = Encoding.UTF8.GetBytes((row.GetProperty("bom").GetBoolean() ? "\uFEFF" : "") + text);
            }
            var source = row.GetProperty("source").GetString()!;
            Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)), Is.EqualTo(hashes[source]), source);
        }
        Assert.That(mappings.RootElement.GetArrayLength(), Is.EqualTo(hashes.Count));
    }
}
