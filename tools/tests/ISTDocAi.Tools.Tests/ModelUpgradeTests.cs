using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using ISTDocAi.OperatorTool;
using ISTDocAi.Provisioning;
using ISTDocAi.Tools.Tests.Provisioning;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class ModelUpgradeTests
{
    internal static OperatorArguments Args(string doc, string id, string token, string mapping = "")
    {
        var values = new Dictionary<string, string> { ["model-id"] = id, ["expected-snapshot"] = token };
        if (doc == "AUTO")
        {
            values["test-accuracy"] = ".99"; values["min-confidence"] = ".5";
            values["bill-class-name"] = "bill"; values["manifest-class-name"] = "manifest"; values["other-class-name"] = "other";
        }
        else { values["document-type-code"] = doc; values["test-field-accuracy"] = ".99"; values["mapping-file"] = mapping; }
        return new("models", doc == "AUTO" ? "upgrade-classifier" : "upgrade-extract", values);
    }
    [TestCase("BL")][TestCase("MANIFEST")][TestCase("AUTO")]
    public void Independent_arguments_need_no_other_groups(string doc)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, OperatorPureTests.Mapping(doc == "AUTO" ? "BL" : doc).ToJsonString());
            var a = Args(doc, "new-model", new string('A', 64), path);
            var parsed = OperatorArguments.Parse(new[] { a.Command, a.Action }.Concat(a.Values.SelectMany(p => new[] { "--" + p.Key, p.Value })).ToArray());
            Assert.That(parsed.Models(), Does.Contain("Active model changed since review.").And.Contain("BEGIN TRANSACTION"));
            a.Values.Remove("expected-snapshot"); Assert.Throws<ArgumentException>(() => a.Models());
        }
        finally { File.Delete(path); }
    }
    [TestCase("bad")][TestCase("")][TestCase("AUTO")]
    public void Invalid_extract_document_type_refused(string doc)
    {
        var a = Args("BL", "new", new string('A', 64)); a.Values["document-type-code"] = doc;
        Assert.Throws<ArgumentException>(() => a.Models());
    }

    [TestCase("bad")][TestCase("ABC")][TestCase("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void Invalid_snapshot_refused_before_file_or_database_IO(string token) => Assert.Throws<ArgumentException>(() => Args("MANIFEST", "new", token, "missing.json").Models());

    [TestCase("--json")][TestCase("--bl-model-id")][TestCase("--mapping-file")]
    public void Classifier_rejects_extract_or_output_options(string option) => Assert.Throws<ArgumentException>(() => OperatorArguments.Parse(["models", "upgrade-classifier", option, "x"]));

    [Test]
    public void Classifier_requires_measurement_confidence_and_distinct_routes()
    {
        foreach (var key in new[] { "test-accuracy", "min-confidence", "bill-class-name", "manifest-class-name", "other-class-name" })
        {
            var a = Args("AUTO", "new", new string('A', 64)); a.Values.Remove(key);
            Assert.Throws<ArgumentException>(() => a.Models());
        }
        var duplicate = Args("AUTO", "new", new string('A', 64)); duplicate.Values["other-class-name"] = "bill";
        Assert.Throws<ArgumentException>(() => duplicate.Models());
    }
    [TestCase("BL")][TestCase("MANIFEST")][TestCase("AUTO")]
    public void Snapshot_is_deterministic_and_auto_has_no_line(string doc)
    {
        var first = ModelUpgradeSql.Active(doc);
        Assert.That(ModelUpgradeSql.Active(doc), Is.EqualTo(first));
        if (doc == "AUTO") Assert.Throws<ArgumentException>(() => ModelUpgradeSql.Active(doc, "LINE"));
    }
    [Test]
    public void Scripts_require_preview_approval_and_have_charging_note()
    {
        var files = ModelUpgradeSql.Files(Args("AUTO", "new", new string('A', 64)));
        Assert.That(files["model-upgrade-preview.sql"], Does.Contain("ROLLBACK;").And.Contain(ModelUpgradeSql.ChargingNote));
        Assert.That(files["model-upgrade.sql"], Does.Contain("@OperatorConfirmed<>1 OR @token<>@ReviewedPreviewToken").And.Contain("@OperatorConfirmed bit=0"));
    }
    // Same activation gates as register-set: BL 0.95, MANIFEST extract and AUTO classifier 0.6 (router gate).
    [TestCase("BL", null, "@minimumAccuracy=0.95")][TestCase("MANIFEST", null, "@minimumAccuracy=0.6")]
    [TestCase("AUTO", null, "@minimumAccuracy=0.6")][TestCase("MANIFEST", ".7", "@minimumAccuracy=0.7")]
    public void Activation_gate_defaults_match_register_set(string doc, string? overrideGate, string expected)
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, OperatorPureTests.Mapping(doc == "AUTO" ? "MANIFEST" : doc).ToJsonString());
            var a = Args(doc, "new-model", new string('A', 64), file);
            if (overrideGate is not null) a.Values[doc == "AUTO" ? "router-minimum-accuracy" : "minimum-accuracy"] = overrideGate;
            Assert.That(ModelUpgradeSql.Generate(a), Does.Contain(expected));
        }
        finally { File.Delete(file); }
    }
    [Test]
    public async Task Generate_sql_CLI_is_offline_and_refuses_overwrite()
    {
        var folder = Path.Combine(RepositoryPaths.ToolsRoot, "tests", "ISTDocAi.Tools.Tests", "TestResults", Guid.NewGuid().ToString("N"));
        try
        {
            var a = Args("AUTO", "new", new string('A', 64)); a.Values["generate-sql"] = folder;
            var args = new[] { a.Command, a.Action }.Concat(a.Values.SelectMany(p => new[] { "--" + p.Key, p.Value })).ToArray();
            var console = new OperatorPureTests.ConsoleStub();
            Assert.That(await OperatorCli.Run(args, console), Is.Zero);
            var bytes = File.ReadAllBytes(Path.Combine(folder, "model-upgrade.sql"));
            Assert.That(await OperatorCli.Run(args, console), Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(Path.Combine(folder, "model-upgrade.sql")), Is.EqualTo(bytes));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}

[Category("Sql")]
public sealed class ModelUpgradeSqlTests : GuardedToolTests
{
    private static async Task<(string Json, string Token)> Snapshot(SqlConnection sql, string doc, string line = "")
    {
        var row = await sql.QuerySingleAsync(ModelUpgradeSql.Active(doc, line));
        return ((string)row.SnapshotJson, (string)row.SNAPSHOT_TOKEN);
    }
    [TestCase("BL", "")][TestCase("MANIFEST", "")][TestCase("AUTO", "")][TestCase("BL", "LINE")]
    public async Task Independent_upgrade_is_atomic_reviewed_and_scripts_replay_safely(string doc, string line)
    {
        await using var db = new ProvisioningDatabase(); await db.Initialize();
        var folder = Path.Combine(RepositoryPaths.ToolsRoot, "tests", "ISTDocAi.Tools.Tests", "TestResults", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var bl = Path.Combine(folder, "bl.json"); var manifest = Path.Combine(folder, "manifest.json");
            File.WriteAllText(bl, OperatorPureTests.Mapping("BL").ToJsonString()); File.WriteAllText(manifest, OperatorPureTests.Mapping().ToJsonString());
            await db.Execute(ModelSql.Set(new("upgrade-old-bl", .99m) { BlMappingFile = bl, ManifestModelId = "upgrade-old-manifest", ManifestTestFieldAccuracy = .99m, ManifestMappingFile = manifest, ClassifierModelId = "upgrade-old-auto", ClassifierTestFieldAccuracy = .99m }));
            await using var sql = new SqlConnection(db.ConnectionString); await sql.OpenAsync();
            var before = await Snapshot(sql, doc, line);
            Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(before));
            Assert.That(before.Token, Is.EqualTo(Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(before.Json)))));
            var untouched = new Dictionary<string, (string Json, string Token)>();
            foreach (var other in new[] { "BL", "MANIFEST", "AUTO" }.Where(x => x != doc)) untouched[other] = await Snapshot(sql, other);
            var a = ModelUpgradeTests.Args(doc, "upgrade-new", before.Token, doc == "BL" ? bl : manifest);
            if (line.Length > 0) a.Values["shipping-line"] = line;
            // Stale review must fail before registration or mapping writes.
            a.Values["expected-snapshot"] = new string('0', 64);
            var stale = Assert.ThrowsAsync<SqlException>(async () => await sql.ExecuteAsync(a.Models()));
            Assert.That(stale!.Number, Is.EqualTo(50018));
            Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(before));
            Assert.That(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ModelRegistry WHERE ModelId='upgrade-new'"), Is.Zero);
            a.Values["expected-snapshot"] = before.Token;
            if (doc != "AUTO")
            {
                // An incompatible pre-existing profile fails inside the mutation transaction.
                await sql.ExecuteAsync($"INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES('upgrade-new',1,'{doc}','ModelProfile','Different','Number','Seal','Bills',N'conflict');");
                var invalid = Assert.ThrowsAsync<SqlException>(async () => await sql.ExecuteAsync(a.Models()));
                Assert.That(invalid!.Number, Is.EqualTo(51205));
                Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(before));
                Assert.That(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ModelRegistry WHERE ModelId='upgrade-new'"), Is.Zero);
                await sql.ExecuteAsync("DELETE dbo.MappingProfiles WHERE ProfileCode='upgrade-new';");
            }
            var files = ModelUpgradeSql.Files(a);
            Assert.ThrowsAsync<SqlException>(async () => await sql.ExecuteAsync(files["model-upgrade.sql"]));
            var previewRows = (await sql.QueryAsync(files["model-upgrade-preview.sql"])).ToArray();
            // QueryAsync reads the first summary set. QueryMultiple consumes the preview token set.
            Assert.That(previewRows, Is.Not.Empty);
            string previewToken;
            using (var results = await sql.QueryMultipleAsync(files["model-upgrade-preview.sql"]))
            {
                _ = (await results.ReadAsync()).ToArray();
                _ = (await results.ReadAsync()).ToArray();
                previewToken = ((string)(await results.ReadSingleAsync()).PreviewToken)[14..];
            }
            Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(before));
            var execute = files["model-upgrade.sql"].Replace("@ReviewedPreviewToken varchar(64)=''", "@ReviewedPreviewToken varchar(64)='" + previewToken + "'", StringComparison.Ordinal).Replace("@OperatorConfirmed bit=0", "@OperatorConfirmed bit=1", StringComparison.Ordinal);
            await sql.ExecuteAsync(execute);
            var after = await Snapshot(sql, doc, line);
            Assert.That(after.Token, Is.Not.EqualTo(before.Token));
            Assert.That(JsonNode.Parse(after.Json)!["Model"]!["ModelId"]!.GetValue<string>(), Is.EqualTo("upgrade-new"));
            if (doc != "AUTO")
            {
                var effective = new AnalyzeResultMappingProfile { DocumentTypeCode = doc, ContainersField = "Containers", ContainerNumberField = "Number", SealNumberField = "Seal", BillsOfLadingField = "Bills", FieldMap = new(StringComparer.Ordinal) { ["shipperName"] = "ShipperName" } };
                Assert.That(JsonNode.Parse(after.Json)!["MappingFingerprint"]!.GetValue<string>(), Is.EqualTo(MappingFingerprint.ComputeEffective(effective, doc)));
            }
            foreach (var (other, snapshot) in untouched) Assert.That(await Snapshot(sql, other), Is.EqualTo(snapshot));
            Assert.ThrowsAsync<SqlException>(async () => await sql.ExecuteAsync(execute));
            Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(after));
            a.Values["expected-snapshot"] = after.Token;
            Assert.ThrowsAsync<SqlException>(async () => await sql.ExecuteAsync(a.Models()));
            Assert.That(await Snapshot(sql, doc, line), Is.EqualTo(after));
        }
        finally { Directory.Delete(folder, true); }
    }
}
