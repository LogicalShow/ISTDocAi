using System.Text.Json.Nodes;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class DocAiSchemaComparisonTests
{
    private static string[] Frozen() => SchemaCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "fresh-schema-security.json"))).Structure;

    private static string[] EquivalentModel()
    {
        var result = new List<string>();
        foreach (var json in Frozen())
        {
            var row = JsonNode.Parse(json)!.AsObject();
            if ((string?)row["table"] == "ModelRegistry" && (string?)row["name"] == "UQ_Model_Version")
            {
                if (DocAiSchemaComparison.TypeCode(row) == "UQ") continue;
                row["is_unique_constraint"] = false;
            }
            if ((string?)row["table"] == "PageQuotaOperatorRequests")
            {
                if (DocAiSchemaComparison.TypeCode(row) == "PK" || (bool?)row["is_primary_key"] == true)
                    row["name"] = "PK_PageQuotaOperatorRequests";
                else if (row.ContainsKey("reftable")) row["name"] = "FK_PageQuotaOperatorRequests_Consumers_ConsumerId";
            }
            result.Add(row.ToJsonString());
        }
        return result.ToArray();
    }

    [Test]
    public void Only_approved_constraint_representation_and_system_names_normalize_to_the_same_catalog()
    {
        Assert.That(DocAiSchemaComparison.Normalize(EquivalentModel(), false),
            Is.EqualTo(DocAiSchemaComparison.Normalize(Frozen(), true)));
    }

    [TestCase("filter_definition")]
    [TestCase("cols")]
    [TestCase("type")]
    public void Nullable_unique_exception_preserves_filter_key_order_and_clustering(string attribute)
    {
        var rows = EquivalentModel().Select(s => JsonNode.Parse(s)!.AsObject()).ToArray();
        var index = rows.Single(r => (string?)r["table"] == "ModelRegistry" && (string?)r["name"] == "UQ_Model_Version");
        if (attribute == "filter_definition") index[attribute] = "ShippingLineCode IS NOT NULL";
        else if (attribute == "type") index[attribute] = 1; // Wrong clustering.
        else index["cols"]!.AsArray()[0]!["key_ordinal"] = 4;
        Assert.That(DocAiSchemaComparison.Normalize(rows.Select(r => r.ToJsonString()), false),
            Is.Not.EqualTo(DocAiSchemaComparison.Normalize(Frozen(), true)));
    }

    [Test]
    public void System_named_exception_preserves_FK_actions_and_target_columns()
    {
        var rows = EquivalentModel().Select(s => JsonNode.Parse(s)!.AsObject()).ToArray();
        var fk = rows.Single(r => (string?)r["table"] == "PageQuotaOperatorRequests" && r.ContainsKey("reftable"));
        fk["delete_referential_action"] = 1;
        fk["cols"]!.AsArray()[0]!["refcol"] = "Code";
        Assert.That(DocAiSchemaComparison.Normalize(rows.Select(r => r.ToJsonString()), false),
            Is.Not.EqualTo(DocAiSchemaComparison.Normalize(Frozen(), true)));
    }

    [Test]
    public void Difference_report_lists_every_missing_and_extra_row_with_its_object()
    {
        var expected = new[] {
            """{"schema":"dbo","table":"MappingProfiles","name":"DocumentTypeCode","is_nullable":false,"collation_name":"SQL_Latin1_General_CP1_CI_AS"}""",
            """{"schema":"dbo","table":"MappingProfiles","name":"MappingShape","is_nullable":false,"collation_name":"SQL_Latin1_General_CP1_CI_AS"}""",
            """{"schema":"dbo","table":"Consumers","column":"QuotaMode","column_id":16,"default_name":"DF_Consumers_QuotaMode"}""" };
        var actual = expected.Select(s => s.Replace("SQL_Latin1_General_CP1_CI_AS", "Latin1_General_100_BIN2", StringComparison.Ordinal)
            .Replace("DF_Consumers_QuotaMode", "WRONG_DEFAULT", StringComparison.Ordinal)).ToArray();
        var report = DocAiSchemaComparison.Differences(expected, actual);
        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("Only in expected (migrated database): 3"));
            Assert.That(report, Does.Contain("Only in actual (EF-model database): 3"));
            Assert.That(report, Does.Contain("[column] dbo.MappingProfiles.DocumentTypeCode"));
            Assert.That(report, Does.Contain("[column] dbo.MappingProfiles.MappingShape"));
            Assert.That(report, Does.Contain("[column order/default] dbo.Consumers.QuotaMode"));
            foreach (var row in expected.Concat(actual)) Assert.That(report, Does.Contain(row));
        });
        Assert.That(DocAiSchemaComparison.Differences(expected, expected.Reverse()), Is.Empty);
    }

    [Test]
    public void Difference_report_preserves_duplicate_row_counts()
    {
        const string row = """{"schema":"dbo","name":"Jobs","temporal_type":0}""";
        var report = DocAiSchemaComparison.Differences([row, row, row], [row]);
        Assert.That(report, Does.Contain("Only in expected (migrated database): 2"));
        Assert.That(report, Does.Contain("Only in actual (EF-model database): 0"));
        Assert.That(report.Split("[table] dbo.Jobs", StringSplitOptions.None), Has.Length.EqualTo(3));
    }
}
