using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class ModelVerdictTests
{
    internal static JsonObject Row(string kind, string key, string azure = "Name") => new() { ["MapKind"] = kind, ["CanonicalKey"] = key, ["AzureName"] = azure };
    internal static (JsonObject Active, JsonObject Draft, JsonObject Review) Data()
    {
        var active = ModelReviewTests.Snapshot();
        active["MappingProfile"]!["MappingFields"]![0]!["CanonicalKey"] = "vesselName";
        active["MappingProfile"]!["MappingFields"]![1]!["CanonicalKey"] = "billOfLadingNumber";
        var model = ModelReviewTests.Model(ModelReviewTests.Fields()); model["selectedDocType"] = "doc";
        var result = ModelDraft.Extract(active, new string('A', 64), ModelReviewTests.Fields(), ModelReviewTests.Fields(), "new", "REF", (_, _) => "ignore");
        var review = result.Review; review["ActiveSnapshot"] = active.DeepClone(); review["DocumentTypeCode"] = "MANIFEST"; review["AzureModel"] = model;
        return (active, result.Draft!, review);
    }
    internal static string Folder() => Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "phase-c-" + Guid.NewGuid().ToString("N"));
    internal static void Save(string folder, JsonObject draft, JsonObject review, bool auto = false)
    {
        Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "review.json"), review.ToJsonString());
        File.WriteAllText(Path.Combine(folder, auto ? "draft-classifier.json" : "draft-mapping.json"), draft.ToJsonString());
    }
    private static UpgradeVerdict Verdict(JsonObject active, JsonObject draft, JsonObject review) => ModelVerdict.Evaluate("MANIFEST", active, draft, review, new());

    [TestCase("BL", "Field", true)]
    // BL container items declare 6 properties without additionalProperties; the product DTO (ContainerFields) is fixed.
    [TestCase("BL", "Container", false)]
    [TestCase("BL", "Header", false)]
    [TestCase("BL", "Bill", false)]
    [TestCase("MANIFEST", "Header", true)]
    [TestCase("MANIFEST", "Bill", true)]
    [TestCase("MANIFEST", "Container", false)]
    public void Scope_is_derived_from_real_schema(string doc, string kind, bool open)
    {
        var scope = new KitContract().Scope(doc, kind);
        Assert.That(scope.Open, Is.EqualTo(open)); Assert.That(scope.Accepts("newKey"), Is.EqualTo(open));
        Assert.That(scope.Accepts("bad-key"), Is.False);
        if (doc == "MANIFEST" && kind == "Container") { Assert.That(scope.Keys, Does.Contain("grossWeight")); Assert.That(scope.Keys, Does.Not.Contain("grossWeightKg")); }
    }
    [Test]
    public void Only_explicit_additional_properties_open_a_scope_with_declared_properties()
    {
        // Declared properties without additionalProperties: fixed (product DTOs are fixed records).
        var schema = JsonNode.Parse("""{"properties":{"fields":{"properties":{"known":{}}}}}""")!.AsObject();
        var contract = new KitContract(new(), schema, new());
        Assert.That(contract.Scope("BL", "Field").Open, Is.False);
        Assert.That(contract.Scope("BL", "Field").Accepts("known"), Is.True);
        Assert.That(contract.Scope("BL", "Field").Accepts("newKey"), Is.False);
        // Explicit schema or true: open. Explicit false: fixed.
        schema["properties"]!["fields"]!["additionalProperties"] = new JsonObject { ["type"] = "object" };
        Assert.That(contract.Scope("BL", "Field").Accepts("newKey"), Is.True);
        schema["properties"]!["fields"]!["additionalProperties"] = true;
        Assert.That(contract.Scope("BL", "Field").Accepts("newKey"), Is.True);
        schema["properties"]!["fields"]!["additionalProperties"] = false;
        Assert.That(contract.Scope("BL", "Field").Accepts("newKey"), Is.False);
        // No declared properties and no additionalProperties: a free dictionary, open.
        schema["properties"]!["fields"] = new JsonObject { ["type"] = "object" };
        Assert.That(contract.Scope("BL", "Field").Open, Is.True);
    }
    [Test]
    public void Embedded_schemas_are_the_product_files_without_copies()
    {
        foreach (var (doc, file) in new[] { ("BL", "canonical-result-v1.schema.json"), ("MANIFEST", "canonical-manifest-result-v1.schema.json") })
        {
            using var resource = typeof(KitContract).Assembly.GetManifestResourceStream("ISTDocAi.OperatorTool.Schema." + doc + ".json")!;
            using var bytes = new MemoryStream(); resource.CopyTo(bytes);
            Assert.That(bytes.ToArray(), Is.EqualTo(File.ReadAllBytes(Path.Combine(RepositoryPaths.ISTDocAiRoot, "ISTDocAi.Application", "Common", "Contracts", "DocAi", "V1", "Schemas", file))));
        }
    }
    [Test]
    public void BL_dictionary_additions_and_removed_consumed_fields_use_the_BL_kit_scope()
    {
        var (active, draft, review) = Data(); active["DocumentTypeCode"] = "BL"; draft["DocumentTypeCode"] = "BL";
        var rows = new JsonArray(Row("Field", "shipperName"));
        draft["Rows"] = rows; active["MappingProfile"]!["MappingFields"] = rows.DeepClone();
        Assert.That(ModelVerdict.Evaluate("BL", active, draft, review, new()).Colour, Is.EqualTo("GREEN"));
        rows.Add(Row("Field", "newKey"));
        Assert.That(ModelVerdict.Evaluate("BL", active, draft, review, new()).Colour, Is.EqualTo("BLUE"));
        rows.RemoveAt(0);
        Assert.That(ModelVerdict.Evaluate("BL", active, draft, review, new()).Colour, Is.EqualTo("YELLOW"));
    }
    [Test]
    public void Kit_keys_and_inheritance_aliases_are_valid_and_document_table_matches_exactly()
    {
        var contract = new KitContract();
        foreach (var type in contract.Kit["resultTypes"]!.AsObject())
        foreach (var scope in type.Value!.AsObject())
        foreach (var key in scope.Value!.AsArray())
            Assert.That(contract.Scope(type.Key == "bill-of-lading.v1" ? "BL" : "MANIFEST", scope.Key).Accepts(key!.ToString()), Is.True, type.Key + "/" + scope.Key + "/" + key);
        var rules = contract.Kit["rules"]!["cargo-manifest.v1"]!;
        Assert.That(JsonNode.DeepEquals(rules["inheritHeader"], contract.Kit["resultTypes"]!["cargo-manifest.v1"]!["Header"]), Is.True);
        Assert.That(rules["billAliases"]!["totalGrossWeightKg"]!.ToString(), Is.EqualTo("grossWeight"));
        Assert.That(rules["containerAliases"]!["grossWeightKg"]!.ToString(), Is.EqualTo("grossWeight"));
        foreach (var key in rules["unsupportedContainerReads"]!.AsArray()) Assert.That(contract.Scope("MANIFEST", "Container").Accepts(key!.ToString()), Is.False);
        var doc = File.ReadAllText(Path.Combine(RepositoryPaths.ToolsRoot, "docs", "KIT-INTEGRATION.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var table = doc.Split("<!-- kit-fields:start -->\n", StringSplitOptions.None)[1].Split("\n<!-- kit-fields:end -->", StringSplitOptions.None)[0];
        Assert.That(table, Is.EqualTo(contract.Table()));
    }
    [Test]
    public void Fixtures_contain_expected_consumed_keys_in_each_scope()
    {
        var contract = new KitContract();
        foreach (var fixture in contract.Kit["fixtures"]!.AsObject())
        {
            var f = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryPaths.ToolsRoot, "..", "ISTDocAi.UI.Server.Tests", "Fixtures", "KitResults", fixture.Key)))!;
            var manifest = fixture.Key != "bl.json";
            var scopes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            void Keys(string scope, JsonNode? values)
            {
                if (!scopes.TryGetValue(scope, out var set)) scopes[scope] = set = new(StringComparer.Ordinal);
                foreach (var key in values!.AsObject().Select(p => p.Key)) set.Add(key);
            }
            if (!manifest) { Keys("Field", f["fields"]); foreach (var c in f["containers"]!.AsArray()) Keys("Container", c); }
            else
            {
                Keys("Header", f["header"]);
                foreach (var b in f["billsOfLading"]!.AsArray().Concat(f["unidentifiedBillsOfLading"]!.AsArray()))
                {
                    Keys("Bill", b!["fields"]);
                    foreach (var c in b["containers"] as JsonArray ?? []) Keys("Container", c);
                }
            }
            foreach (var scope in fixture.Value!["expectedKeys"]!.AsObject())
            {
                Assert.That(scope.Value!.AsArray().Count, Is.GreaterThan(0));
                foreach (var key in scope.Value.AsArray()) Assert.That(scopes[scope.Key], Does.Contain(key!.ToString()));
            }
        }
    }
    [TestCase("GREEN")][TestCase("BLUE")][TestCase("YELLOW")][TestCase("RED")][TestCase("WHITE")]
    public void Each_colour_and_final_rows_drive_verdict(string colour)
    {
        var (active, draft, review) = Data();
        if (colour == "BLUE") draft["Rows"]!.AsArray().Add(Row("Header", "newKey"));
        if (colour == "YELLOW") draft["Rows"]!.AsArray().RemoveAt(0);
        if (colour == "RED") draft["Rows"]!.AsArray().Add(Row("Container", "invalidFixedKey", "Number"));
        if (colour == "WHITE") review["Items"]!.AsArray().Add(new JsonObject { ["Status"] = "ignored", ["MapKind"] = "Header", ["AzureName"] = "NewIgnored" });
        var result = Verdict(active, draft, review);
        Assert.That(result.Items.Select(i => i.Colour), Does.Contain(colour));
        Assert.That(result.Colour, Is.EqualTo(colour == "WHITE" ? "GREEN" : colour));
        if (colour == "BLUE") Assert.That(result.Table, Does.Contain("change + publish").And.Contain("newKey"));
        if (colour == "YELLOW") Assert.That(result.Table, Does.Contain("vesselName"));
        if (colour == "RED") Assert.That(result.Table, Does.Contain("invalidFixedKey").And.Contain("ISTDocAi").And.Contain("kit"));
    }
    [Test]
    public void Mixed_severities_and_blocked_items_refuse_even_if_mapping_was_edited()
    {
        var (active, draft, review) = Data(); draft["Rows"]!.AsArray().RemoveAt(0); draft["Rows"]!.AsArray().Add(Row("Header", "newKey"));
        Assert.That(Verdict(active, draft, review).Colour, Is.EqualTo("YELLOW"));
        review["Items"]!.AsArray().Add(new JsonObject { ["Status"] = "blocked", ["MapKind"] = "Selector", ["AzureName"] = "Containers", ["Reason"] = "Expected array of objects." });
        Assert.That(Verdict(active, draft, review).Colour, Is.EqualTo("RED"));
        Assert.That(ModelVerdict.Evaluate("MANIFEST", active, null, review, new()).Colour, Is.EqualTo("RED"));
    }
    [Test]
    public void Restored_key_avoids_yellow_and_remapped_ignored_field_avoids_white()
    {
        var (active, draft, review) = Data();
        review["Items"]!.AsArray().Add(new JsonObject { ["Status"] = "removed", ["MapKind"] = "Header", ["CanonicalKey"] = "vesselName" });
        review["Items"]!.AsArray().Add(new JsonObject { ["Status"] = "ignored", ["MapKind"] = "Header", ["AzureName"] = "Name" });
        Assert.That(Verdict(active, draft, review).Colour, Is.EqualTo("GREEN"));
        Assert.That(Verdict(active, draft, review).Items.Any(i => i.Colour == "WHITE"), Is.False);
    }
    [TestCase("missing")][TestCase("object")][TestCase("selector")][TestCase("fallback")]
    public void Edited_Azure_paths_and_structural_selectors_are_revalidated(string change)
    {
        var (active, draft, review) = Data();
        if (change == "missing") draft["Rows"]![0]!["AzureName"] = "Missing";
        if (change == "object") draft["Rows"]![0]!["AzureName"] = "Containers";
        if (change == "selector") draft["ContainersField"] = "Missing";
        if (change == "fallback") { draft["Rows"]!.AsArray().RemoveAt(2); draft["ContainerNumberField"] = "Missing"; }
        Assert.That(Verdict(active, draft, review).Colour, Is.EqualTo("RED"));
    }
    internal static (JsonObject Active, JsonObject Draft, JsonObject Review) Auto()
    {
        var active = new JsonObject { ["DocumentTypeCode"] = "AUTO", ["Model"] = new JsonObject { ["ModelId"] = "old" } };
        var draft = new JsonObject { ["bill-class-name"] = "bill", ["manifest-class-name"] = "manifest", ["other-class-name"] = "other", ["bill-shipping-line"] = "LINE" };
        var review = new JsonObject { ["NewModelId"] = "new", ["OldModelId"] = "old", ["SNAPSHOT_TOKEN"] = new string('A', 64), ["ActiveSnapshot"] = active.DeepClone(), ["Items"] = new JsonArray(), ["AzureModel"] = new JsonObject { ["docTypes"] = new JsonObject { ["bill"] = new JsonObject(), ["manifest"] = new JsonObject(), ["other"] = new JsonObject(), ["unused"] = new JsonObject() } } };
        return (active, draft, review);
    }
    [TestCase("complete")][TestCase("missing")][TestCase("duplicate")][TestCase("unknown")]
    public void Auto_requires_three_distinct_available_routes(string mode)
    {
        var (active, draft, review) = Auto();
        if (mode == "missing") draft.Remove("other-class-name");
        if (mode == "duplicate") draft["other-class-name"] = "bill";
        if (mode == "unknown") draft["other-class-name"] = "absent";
        var verdict = ModelVerdict.Evaluate("AUTO", active, draft, review, new());
        Assert.That(verdict.Colour, Is.EqualTo(mode == "complete" ? "GREEN" : "RED"));
        if (mode == "complete") Assert.That(verdict.Table, Does.Contain("WHITE").And.Contain("unused").And.Contain("do not change result fields"));
    }
    [TestCase("GREEN", 0)][TestCase("BLUE", 0)][TestCase("YELLOW", 2)][TestCase("RED", 1)]
    public async Task Check_exit_codes_and_outputs_use_edited_draft(string colour, int code)
    {
        var (_, draft, review) = Data(); var folder = Folder();
        try
        {
            Save(folder, draft, review); var c = new ModelReviewTests.Input();
            var args = new[] { "models", "check", "--document-type-code", "MANIFEST", "--review", folder };
            Assert.That(await OperatorCli.Run(args, c), Is.Zero);
            if (colour == "BLUE") draft["Rows"]!.AsArray().Add(Row("Header", "newKey"));
            if (colour == "YELLOW") draft["Rows"]!.AsArray().RemoveAt(0);
            if (colour == "RED") draft["Rows"]![0]!["CanonicalKey"] = "bad-key";
            var edited = Path.Combine(folder, "edited.json"); File.WriteAllText(edited, draft.ToJsonString());
            Assert.That(await OperatorCli.Run(args.Concat(["--mapping-file", edited, "--sql-server", "fake"]).ToArray(), c), Is.EqualTo(code));
            Assert.That(File.ReadAllText(Path.Combine(folder, "verdict.txt")), Does.Contain(colour));
            Assert.That(JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "verdict.json")))!["Colour"]!.ToString(), Is.EqualTo(colour));
        }
        finally { Directory.Delete(folder, true); }
    }
    [TestCase("duplicate")][TestCase("unknown")][TestCase("brokenJson")][TestCase("wrongDoc")]
    public void Check_rejects_binding_violations_and_invalid_json(string mode)
    {
        var (_, draft, review) = Data(); var folder = Folder();
        try
        {
            if (mode == "duplicate") draft["Rows"]!.AsArray().Add(draft["Rows"]![0]!.DeepClone());
            if (mode == "unknown") draft["Extra"] = true;
            if (mode == "wrongDoc") draft["DocumentTypeCode"] = "BL";
            Save(folder, draft, review);
            if (mode == "brokenJson") File.WriteAllText(Path.Combine(folder, "draft-mapping.json"), "{");
            Assert.That(ModelCheck.Read("MANIFEST", folder).Verdict.Colour, Is.EqualTo("RED"));
        }
        finally { Directory.Delete(folder, true); }
    }
    [TestCase(true, 0)][TestCase(false, 1)]
    public async Task Check_auto_and_white_items_do_not_need_Azure_or_SQL(bool complete, int exit)
    {
        var (_, draft, review) = Auto(); var folder = Folder();
        try
        {
            if (!complete) draft.Remove("manifest-class-name");
            Save(folder, draft, review, true);
            var c = new ModelReviewTests.Input();
            Assert.That(await OperatorCli.Run(["models", "check", "--document-type-code", "AUTO", "--review", folder], c), Is.EqualTo(exit));
            Assert.That(string.Join("\n", c.Output), Does.Contain(complete ? "WHITE" : "RED"));
        }
        finally { Directory.Delete(folder, true); }
    }
    [TestCase("0.94", "0.90", false)][TestCase("0.95", "0.90", false)][TestCase("0.951", "0.90", true)][TestCase("0.849", "0.90", true)]
    public void Accuracy_gap_is_strictly_five_points(string measured, string estimate, bool gap) => Assert.That(UpgradeAccuracy.Gap(decimal.Parse(measured, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(estimate, System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(gap));
    [Test]
    public void Estimate_uses_only_final_mapped_available_confidences()
    {
        var (_, draft, review) = Data(); var fields = DocumentModelClient.Fields(review["AzureModel"]!.AsObject());
        fields["Name"]!["confidence"] = .9m; fields["Bills"]!["items"]!["properties"]!["Bill"]!["confidence"] = .7m;
        fields["Unmapped"] = new JsonObject { ["type"] = "string", ["confidence"] = .1m };
        Assert.That(UpgradeAccuracy.Estimate("MANIFEST", draft, review), Is.EqualTo(.8m));
        draft["Rows"]!.AsArray().RemoveAt(0);
        Assert.That(UpgradeAccuracy.Estimate("MANIFEST", draft, review), Is.EqualTo(.7m));
        draft["Rows"]!.AsArray().RemoveAt(0);
        Assert.That(UpgradeAccuracy.Estimate("MANIFEST", draft, review), Is.Null);
        Assert.That(UpgradeAccuracy.Estimate("AUTO", draft, review), Is.Null);
        Assert.That(UpgradeAccuracy.Gap(.9m, null), Is.False);
        Assert.Throws<ArgumentException>(() => UpgradeAccuracy.Gap(1.1m, null));
    }
    [Test]
    public void Estimate_includes_effective_fallback_selectors_when_override_rows_are_removed()
    {
        var (_, draft, review) = Data(); draft["Rows"] = new JsonArray();
        var fields = DocumentModelClient.Fields(review["AzureModel"]!.AsObject());
        fields["Containers"]!["items"]!["properties"]!["Number"]!["confidence"] = .92m;
        fields["Containers"]!["items"]!["properties"]!["Seal"]!["confidence"] = .88m;
        Assert.That(UpgradeAccuracy.Estimate("MANIFEST", draft, review), Is.EqualTo(.9m));
    }
    [TestCase("")][TestCase(" ")][TestCase("bad\nref")][TestCase("$(secret)")]
    public void Evidence_is_required_and_obeys_upgrade_validation(string evidence) => Assert.Throws<ArgumentException>(() => UpgradeAccuracy.Evidence(evidence));
}
