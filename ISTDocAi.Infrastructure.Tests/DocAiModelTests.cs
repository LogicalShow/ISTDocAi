using System.Text.Json.Nodes;
using System.Text;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class DocAiModelTests
{
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ISTDocAi.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Test]
    public void Snapshot_matches_model_and_only_two_frozen_migrations_exist()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        Assert.That(context.Database.HasPendingModelChanges(), Is.False);
        Assert.That(context.Database.GetMigrations(), Is.EqualTo(new[] {
            "20261101000000_InitialSchema", "20261103000000_ModelMappingProfiles" }));
        var files = Directory.GetFiles(Path.Combine(Root, "ISTDocAi.Infrastructure", "Migrations", "DocAi"), "*.cs")
            .Select(Path.GetFileName).Order().ToArray();
        Assert.That(files, Is.EquivalentTo(new[] { "20261101000000_InitialSchema.cs",
            "20261101000000_InitialSchema.Designer.cs", "20261103000000_ModelMappingProfiles.cs",
            "DocAiDbContextModelSnapshot.cs" }));
    }

    [Test]
    public void Contexts_are_isolated_and_model_has_only_the_twenty_frozen_tables_and_declared_triggers()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        using var iam = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var expected = SchemaCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fresh-schema-security.json"))).Structure.Select(s => JsonNode.Parse(s)!.AsObject()).ToArray();
        var tables = expected.Where(r => r.ContainsKey("temporal_type")).Select(r => (string)r["name"]!).Order().ToArray();
        Assert.That(tables, Has.Length.EqualTo(20));
        Assert.That(model.GetEntityTypes().Select(e => e.GetTableName()).Order(), Is.EqualTo(tables));
        Assert.That(model.GetEntityTypes().All(e => e.GetSchema() == "dbo"), Is.True);
        Assert.That(iam.Model.GetEntityTypes().Select(e => e.GetTableName()).Intersect(tables), Is.Empty);
        // SQL Server's sys.objects type is char(2); handle its padded representation explicitly.
        var triggers = expected.Where(r => DocAiSchemaComparison.TypeCode(r)?.Trim() == "TR").Select(r => (string)r["name"]!).Order().ToArray();
        Assert.That(model.GetEntityTypes().SelectMany(e => e.GetDeclaredTriggers()).Select(t => t.ModelName).Order(), Is.EqualTo(triggers));
        var expectedIndexes = expected.Where(r => r.ContainsKey("is_primary_key") && !(bool)r["is_primary_key"]! &&
            (!(bool)r["is_unique_constraint"]! || (string?)r["name"] == "UQ_Model_Version"))
            .Select(r => $"{r["table"]}.{r["name"]}").Order();
        Assert.That(model.GetEntityTypes().SelectMany(e => e.GetIndexes().Select(i => $"{e.GetTableName()}.{i.GetDatabaseName()}")).Order(),
            Is.EqualTo(expectedIndexes), "No missing/extra conventional FK indexes.");
        foreach (var entity in model.GetEntityTypes())
        {
            var columns = expected.Where(r => r.ContainsKey("is_nullable") && (string?)r["table"] == entity.GetTableName()).ToArray();
            Assert.That(entity.GetProperties().Select(p => p.GetColumnName()).Order(), Is.EqualTo(columns.Select(c => (string)c["name"]!).Order()));
            foreach (var property in entity.GetProperties())
            {
                var column = columns.Single(c => (string?)c["name"] == property.GetColumnName());
                var type = (string)column["type"]!;
                var length = (int)column["max_length"]!;
                var sqlType = type switch
                {
                    "timestamp" => "rowversion",
                    "char" or "varchar" or "nvarchar" or "binary" or "varbinary" => type + "(" +
                        (length == -1 ? "max" : (type == "nvarchar" ? length / 2 : length).ToString(System.Globalization.CultureInfo.InvariantCulture)) + ")",
                    "decimal" => $"decimal({column["precision"]},{column["scale"]})",
                    "datetime2" => $"datetime2({column["scale"]})",
                    _ => type
                };
                Assert.That(property.GetColumnType(), Is.EqualTo(sqlType), $"{entity.GetTableName()}.{property.GetColumnName()} type");
                Assert.That(property.IsNullable, Is.EqualTo((bool)column["is_nullable"]!), $"{entity.GetTableName()}.{property.GetColumnName()} nullability");
                var frozenCollation = (string?)column["collation_name"];
                var explicitCollation = frozenCollation == "Latin1_General_100_BIN2" ? frozenCollation : null;
                Assert.That(property.GetCollation(), Is.EqualTo(explicitCollation),
                    $"{entity.GetTableName()}.{property.GetColumnName()} column-level collation (CHECK collation is separate)");
            }
        }
    }

    [Test, Category("Sql")]
    public async Task Frozen_initializer_and_EF_model_have_identical_schema_except_explicit_metadata_allowlists()
    {
        SqlTestGuard.Require(); // Ignore before constructing/opening any connection.
        using var assertions = Assert.EnterMultipleScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var ct = timeout.Token;
        await using var golden = new DisposableDatabase();
        await using var modeled = new DisposableDatabase();
        await new DatabaseInitializer().InitializeAsync(new(), golden.ConnectionString, _ => { }, ct);
        await modeled.Create(ct);
        await using (var context = modeled.Context())
            await modeled.ExecuteBatches(SqlBatchReader.Split(context.Database.GenerateCreateScript()), ct);
        var expected = await Read(golden.ConnectionString, true, ct);
        var actual = await Read(modeled.ConnectionString, false, ct);
        var differences = DocAiSchemaComparison.Differences(expected, actual);
        if (differences.Length != 0)
        {
            TestContext.Out.WriteLine(differences);
            Assert.Fail("Only the two explicitly approved metadata classes may differ.\n" + differences);
        }
    }

    private static async Task<string[]> Read(string connectionString, bool golden, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var catalog = await SchemaCatalog.ReadAsync(connection, null, 300, ct, fullSecurity: false);
        var structure = DocAiSchemaComparison.Normalize(catalog.Structure, golden).ToList();
        // SchemaCatalog intentionally remains frozen. Supplement it with column ordinal and default NAME metadata.
        await using var command = new SqlCommand("""
            SELECT (SELECT s.name AS [schema],t.name AS [table],c.name AS [column],c.column_id,
              d.name AS default_name,d.is_system_named AS default_system_named
              FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id
            LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
            WHERE t.name<>'__EFMigrationsHistory';
            SELECT (SELECT s.name AS [schema],t.name AS [table],RTRIM(o.type) AS kind,o.name,
              CASE o.type WHEN 'PK' THEN k.is_system_named WHEN 'UQ' THEN k.is_system_named
                WHEN 'F' THEN f.is_system_named WHEN 'D' THEN d.is_system_named
                WHEN 'C' THEN ch.is_system_named END AS is_system_named,
              CASE WHEN o.type IN ('PK','UQ') THEN
                (SELECT c.name FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                  WHERE ic.object_id=t.object_id AND ic.index_id=k.unique_index_id ORDER BY ic.key_ordinal FOR JSON PATH)
              WHEN o.type='F' THEN
                (SELECT COL_NAME(fc.parent_object_id,fc.parent_column_id) AS name FROM sys.foreign_key_columns fc
                  WHERE fc.constraint_object_id=o.object_id ORDER BY fc.constraint_column_id FOR JSON PATH)
              ELSE (SELECT COL_NAME(o.parent_object_id,COALESCE(d.parent_column_id,ch.parent_column_id)) AS name FOR JSON PATH) END AS cols
              FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
            FROM sys.objects o JOIN sys.tables t ON t.object_id=o.parent_object_id
            JOIN sys.schemas s ON s.schema_id=t.schema_id
            LEFT JOIN sys.key_constraints k ON k.object_id=o.object_id
            LEFT JOIN sys.foreign_keys f ON f.object_id=o.object_id
            LEFT JOIN sys.default_constraints d ON d.object_id=o.object_id
            LEFT JOIN sys.check_constraints ch ON ch.object_id=o.object_id
            WHERE o.type IN ('PK','UQ','F','D','C') AND t.name<>'__EFMigrationsHistory';
            """, connection) { CommandTimeout = 300 };
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonNode.Parse(reader.GetString(0))!.AsObject();
            if ((string?)row["schema"] == "dbo" && (string?)row["table"] == "PageQuotaOperatorRequests" && (string?)row["column"] == "RecordedAtUtc")
            {
                Assert.That((bool?)row["default_system_named"], Is.EqualTo(golden));
                if (!golden) Assert.That((string?)row["default_name"], Is.EqualTo("DF_PageQuotaOperatorRequests_RecordedAtUtc"));
                row["default_name"] = null;
                row["default_system_named"] = true;
            }
            structure.Add(row.ToJsonString());
        }
        await reader.NextResultAsync(ct);
        var systems = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            var row = JsonNode.Parse(reader.GetString(0))!.AsObject();
            var signature = DocAiSchemaComparison.ConstraintSignature(row);
            if ((bool)row["is_system_named"]!) systems.Add(signature);
            // Enumerate all constraints, including system-named ones. Default names are compared above;
            // PK/UQ/FK/CHECK names and all structural attributes are compared by SchemaCatalog.
            if (DocAiSchemaComparison.SystemNamedObjects.Contains(signature))
            {
                Assert.That((bool)row["is_system_named"]!, Is.EqualTo(golden), signature);
                if (!golden) Assert.That((string?)row["name"], Is.EqualTo(DocAiSchemaComparison.ModelConstraintNames[signature]));
                TestContext.Out.WriteLine($"Allowlisted {signature}: {row["name"]}");
            }
            else Assert.That((bool)row["is_system_named"]!, Is.False, "Unexpected system-named constraint: " + signature);
        }
        Assert.That(systems, Is.EquivalentTo(golden ? DocAiSchemaComparison.SystemNamedObjects : []));
        return structure.Order(StringComparer.Ordinal).ToArray();
    }
}

internal static class DocAiSchemaComparison
{
    // Exactly the three unnamed constraints found in BOTH frozen SQL sources; the live inventory must match.
    internal static readonly string[] SystemNamedObjects = [
        "dbo.PageQuotaOperatorRequests|PK|RequestId",
        "dbo.PageQuotaOperatorRequests|F|ConsumerId",
        "dbo.PageQuotaOperatorRequests|D|RecordedAtUtc"];
    internal static readonly IReadOnlyDictionary<string, string> ModelConstraintNames = new Dictionary<string, string>
    {
        [SystemNamedObjects[0]] = "PK_PageQuotaOperatorRequests",
        [SystemNamedObjects[1]] = "FK_PageQuotaOperatorRequests_Consumers_ConsumerId",
        [SystemNamedObjects[2]] = "DF_PageQuotaOperatorRequests_RecordedAtUtc"
    };
    internal static string? TypeCode(JsonObject row) => row["type"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static JsonArray Columns(JsonObject row) => row["cols"] is JsonArray array ? array : JsonNode.Parse((string)row["cols"]!)!.AsArray();
    internal static string ConstraintSignature(JsonObject row) =>
        $"{row["schema"]}.{row["table"]}|{row["kind"]}|" + string.Join(",", Columns(row).Select(c => (string)c!["name"]!));

    // Object-type exclusions, never name patterns: SQL procedures P/PC, views V, functions FN/IF/TF/FS/FT,
    // triggers TR/TA. Security principals/roles and permissions are excluded as the separate Security class;
    // database options (including READ_COMMITTED_SNAPSHOT) are outside this table-model comparison.
    private static readonly HashSet<string> ExcludedModuleTypes = ["P", "PC", "V", "FN", "IF", "TF", "FS", "FT", "TR", "TA"];

    // Compare multisets: duplicate/missing catalog rows must stay visible, not vanish through Except().
    internal static string Differences(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedCounts = expected.GroupBy(r => r, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var actualCounts = actual.GroupBy(r => r, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var onlyExpected = expectedCounts.SelectMany(pair => Enumerable.Repeat(pair.Key,
            Math.Max(0, pair.Value - actualCounts.GetValueOrDefault(pair.Key)))).Order(StringComparer.Ordinal).ToArray();
        var onlyActual = actualCounts.SelectMany(pair => Enumerable.Repeat(pair.Key,
            Math.Max(0, pair.Value - expectedCounts.GetValueOrDefault(pair.Key)))).Order(StringComparer.Ordinal).ToArray();
        if (onlyExpected.Length == 0 && onlyActual.Length == 0) return "";
        var report = new StringBuilder();
        Append("Only in expected (migrated database)", onlyExpected);
        Append("Only in actual (EF-model database)", onlyActual);
        return report.ToString();

        void Append(string heading, string[] rows)
        {
            report.AppendLine($"{heading}: {rows.Length}");
            foreach (var json in rows)
            {
                var row = JsonNode.Parse(json)!.AsObject();
                var table = $"{row["schema"]}.{row["table"] ?? row["name"]}";
                var name = (string?)row["column"] ?? (string?)row["name"] ?? "<system-named>";
                var kind = row.ContainsKey("is_nullable") ? "column" : row.ContainsKey("column_id") ? "column order/default" :
                    row.ContainsKey("is_primary_key") ? "index" : row.ContainsKey("reftable") ? "foreign key" :
                    TypeCode(row) is "PK" or "UQ" ? "key constraint" : row.ContainsKey("definition") ? "check constraint" : "table";
                report.AppendLine($"  [{kind}] {table}" + (kind == "table" ? "" : $".{name}"));
                report.AppendLine("    " + json);
            }
        }
    }

    internal static string[] Normalize(IEnumerable<string> rows, bool golden)
    {
        var result = new List<string>();
        var uniqueConstraints = 0;
        var uniqueIndexes = 0;
        var systemPk = 0;
        var systemFk = 0;
        var systemPkIndex = 0;
        foreach (var json in rows)
        {
            var row = JsonNode.Parse(json)!.AsObject();
            if (row.ContainsKey("uses_ansi_nulls"))
            {
                Assert.That(ExcludedModuleTypes, Does.Contain(((string)row["type"]!).Trim()), "Unclassified module type");
                continue;
            }
            if ((string?)row["schema"] == "dbo" && (string?)row["table"] == "ModelRegistry" && (string?)row["name"] == "UQ_Model_Version")
            {
                if (TypeCode(row) == "UQ")
                {
                    Assert.That(golden, Is.True);
                    Assert.That(row["cols"]?.ToJsonString(), Is.EqualTo("[{\"name\":\"DocumentTypeCode\",\"key_ordinal\":1},{\"name\":\"ShippingLineCode\",\"key_ordinal\":2},{\"name\":\"Kind\",\"key_ordinal\":3},{\"name\":\"Version\",\"key_ordinal\":4}]"));
                    uniqueConstraints++;
                    continue; // Its backing index below still compares every name/key/order/filter/clustering attribute.
                }
                Assert.That((bool?)row["is_unique_constraint"], Is.EqualTo(golden));
                Assert.That((bool?)row["is_unique"], Is.True);
                row["is_unique_constraint"] = false;
                uniqueIndexes++;
            }
            if ((string?)row["schema"] == "dbo" && (string?)row["table"] == "PageQuotaOperatorRequests")
            {
                if (TypeCode(row) == "PK")
                {
                    Assert.That((string?)row["name"], Is.EqualTo(golden ? null : ModelConstraintNames[SystemNamedObjects[0]]));
                    row["name"] = null; systemPk++;
                }
                else if (row.ContainsKey("reftable"))
                {
                    Assert.That((string?)row["name"], Is.EqualTo(golden ? null : ModelConstraintNames[SystemNamedObjects[1]]));
                    row["name"] = null; systemFk++;
                }
                else if (row.ContainsKey("is_primary_key") && (bool)row["is_primary_key"]!)
                {
                    Assert.That((string?)row["name"], Is.EqualTo(golden ? null : ModelConstraintNames[SystemNamedObjects[0]]));
                    row["name"] = null; systemPkIndex++;
                }
            }
            result.Add(row.ToJsonString());
        }
        Assert.That(uniqueConstraints, Is.EqualTo(golden ? 1 : 0));
        Assert.That(uniqueIndexes, Is.EqualTo(1));
        Assert.That(systemPk, Is.EqualTo(1));
        Assert.That(systemFk, Is.EqualTo(1));
        Assert.That(systemPkIndex, Is.EqualTo(1));
        return result.Order(StringComparer.Ordinal).ToArray();
    }
}
