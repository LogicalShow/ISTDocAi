using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public sealed record VerdictItem(string Colour, string Scope, string Key, string Message);
public sealed record UpgradeVerdict(string Colour, IReadOnlyList<VerdictItem> Items)
{
    public int ExitCode => Colour == "RED" ? 1 : Colour == "YELLOW" ? 2 : 0;
    public string Table => "Colour | Scope | Key | Action\n" + string.Join("\n", Items.Select(i => $"{i.Colour} | {i.Scope} | {i.Key} | {i.Message}"));
}

public static class ModelVerdict
{
    public static string S(JsonNode? n, string key) => n?[key]?.ToString() ?? "";
    public static JsonObject SourceFields(JsonObject model, JsonObject mapping, string kind)
    {
        var fields = DocumentModelClient.Fields(model);
        return kind switch
        {
            "Container" => fields[S(mapping, "ContainersField")]?["items"]?["properties"] as JsonObject ?? new(),
            "Bill" => fields[S(mapping, "BillsOfLadingField")]?["items"]?["properties"] as JsonObject ?? new(),
            _ => fields
        };
    }
    public static UpgradeVerdict Evaluate(string doc, JsonObject active, JsonObject? draft, JsonObject review, KitContract contract)
    {
        var items = new List<VerdictItem>();
        void Add(string colour, string scope, string key, string message) => items.Add(new(colour, scope, key, message));
        void Red(string scope, string key, string reason) => Add("RED", scope, key, reason + " Required: update ISTDocAi contract/schema, mapping projection and tests where needed; update kit projector/review/save support and publish before activation.");
        foreach (var item in (review["Items"] as JsonArray ?? []))
            if (S(item, "Status") is "blocked" or "structural") Red(S(item, "MapKind"), S(item, "AzureName"), S(item, "Reason"));
        if (draft is null) Red("Draft", "", "No usable final draft.");
        else if (doc == "AUTO")
        {
            try
            {
                ValidateRoutes(draft);
                var classes = review["AzureModel"]?["docTypes"] as JsonObject;
                if (classes is not null)
                {
                    var used = RouteNames.Select(n => S(draft, n)).ToHashSet(StringComparer.Ordinal);
                    foreach (var name in used) if (!classes.ContainsKey(name)) Red("Route", name, "Class absent from Azure classifier; choose an available class for each bill/manifest/other route.");
                    foreach (var name in classes.Select(c => c.Key).Where(n => !used.Contains(n))) Add("WHITE", "Class", name, "Unused Azure class.");
                }
            }
            catch (ArgumentException e) { Red("Routes", "", e.Message); }
        }
        else
        {
            try { ModelSql.Binding(S(review, "NewModelId"), doc, draft.ToJsonString()); }
            catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException) { Red("Mapping", "", "Invalid final mapping: " + e.Message); }
            if (draft["Rows"] is JsonArray rows)
            {
                string EffectiveKind(string kind) => doc == "MANIFEST" && kind == "Field" ? "Header" : kind;
                string Identity(JsonNode r) => EffectiveKind(S(r, "MapKind")) + ":" + S(r, "CanonicalKey");
                var old = active["MappingProfile"]?["MappingFields"] as JsonArray ?? [];
                var before = old.Where(r => r is JsonObject).Select(r => Identity(r!)).ToHashSet(StringComparer.Ordinal);
                var after = rows.Where(r => r is JsonObject).Select(r => Identity(r!)).ToHashSet(StringComparer.Ordinal);
                // Container number/seal selectors are effective mappings even without override rows.
                foreach (var key in new[] { "containerNumber", "sealNumber" }) { before.Add("Container:" + key); after.Add("Container:" + key); }
                foreach (var row in rows.OfType<JsonObject>())
                {
                    var kind = S(row, "MapKind"); var key = S(row, "CanonicalKey");
                    var scope = contract.Scope(doc, kind);
                    if (!scope.Accepts(key)) Red(kind, key, $"{key} is outside the {doc}/{kind} contract property set. Add this exact property to the contract and its serializer/projection.");
                    else if (!before.Contains(Identity(row)) && scope.Open && !contract.Consumes(doc, kind, key))
                        Add("BLUE", kind, key, "The service accepts it; the kit needs a change + publish to show/save " + key + ".");
                }
                foreach (var row in old.OfType<JsonObject>())
                    if (!after.Contains(Identity(row)) && contract.Consumes(doc, S(row, "MapKind"), S(row, "CanonicalKey"))) Add("YELLOW", S(row, "MapKind"), S(row, "CanonicalKey"), "Previously produced kit field is no longer produced. Type yes after reviewing this list.");
                if (review["AzureModel"] is JsonObject model)
                {
                    foreach (var row in rows.OfType<JsonObject>())
                    {
                        var field = SourceFields(model, draft, S(row, "MapKind"))[S(row, "AzureName")];
                        if (field is null || S(field, "type") is "array" or "object" or "") Red(S(row, "MapKind"), S(row, "AzureName"), "Final mapped Azure field is missing or not scalar; correct this mapping row.");
                    }
                    foreach (var selector in doc == "MANIFEST" ? new[] { "ContainersField", "BillsOfLadingField" } : new[] { "ContainersField" })
                    {
                        var field = DocumentModelClient.Fields(model)[S(draft, selector)];
                        if (S(field, "type") != "array" || S(field?["items"], "type") != "object") Red("Selector", selector, "Final selector must name an Azure array of objects.");
                    }
                    foreach (var (key, selector) in new[] { ("containerNumber", "ContainerNumberField"), ("sealNumber", "SealNumberField") })
                    {
                        var name = rows.FirstOrDefault(r => S(r, "MapKind") == "Container" && S(r, "CanonicalKey") == key)?["AzureName"]?.ToString() ?? S(draft, selector);
                        var field = SourceFields(model, draft, "Container")[name];
                        if (field is null || S(field, "type") is "array" or "object" or "") Red("Container", key, "Effective selector missing or not scalar: " + name);
                    }
                }
                foreach (var item in review["Items"] as JsonArray ?? [])
                    if (S(item, "Status") is "ignored" or "new" && !rows.Any(r => S(r, "MapKind") == S(item, "MapKind") && S(r, "AzureName") == S(item, "AzureName"))) Add("WHITE", S(item, "MapKind"), S(item, "AzureName"), "Ignored new Azure field.");
            }
        }
        if (items.All(i => i.Colour is "WHITE")) Add("GREEN", "", "", doc == "AUTO" ? "Classifier changes do not change result fields." : "No kit-impact changes.");
        var severity = new[] { "WHITE", "GREEN", "BLUE", "YELLOW", "RED" };
        return new(items.OrderByDescending(i => Array.IndexOf(severity, i.Colour)).First().Colour, items);
    }
    public static readonly string[] RouteNames = ["bill-class-name", "manifest-class-name", "other-class-name"];
    public static void ValidateRoutes(JsonObject routes)
    {
        if (routes.Any(p => !RouteNames.Contains(p.Key) && p.Key != "bill-shipping-line")) throw new ArgumentException("Unknown classifier route property.");
        foreach (var name in RouteNames) ModelSql.Identifier(S(routes, name));
        if (RouteNames.Select(n => S(routes, n)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) throw new ArgumentException("Complete distinct bill/manifest/other routes required.");
        if (S(routes, "bill-shipping-line").Length > 0) ModelSql.Identifier(S(routes, "bill-shipping-line"), 20);
    }
}

public static class UpgradeAccuracy
{
    public static void Evidence(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence) || evidence.Length > 400 || evidence.Any(char.IsControl) || evidence.Contains("$(", StringComparison.Ordinal)) throw new ArgumentException("Evidence requires 1-400 printable characters without SQLCMD substitutions.");
    }
    public static bool Gap(decimal measured, decimal? estimate)
    {
        if (measured is < 0 or > 1) throw new ArgumentException("Measured accuracy must be between 0 and 1.");
        return estimate.HasValue && Math.Abs(measured - estimate.Value) > .05m;
    }
    public static decimal? Estimate(string doc, JsonObject draft, JsonObject review)
    {
        if (doc == "AUTO" || review["AzureModel"] is not JsonObject model) return null;
        var values = new List<decimal>();
        void Include(JsonNode? confidence)
        {
            if (decimal.TryParse(confidence?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 1) values.Add(n);
        }
        foreach (var row in draft["Rows"]!.AsArray())
        {
            var confidence = ModelVerdict.SourceFields(model, draft, ModelVerdict.S(row, "MapKind"))[ModelVerdict.S(row, "AzureName")]?["confidence"];
            Include(confidence);
        }
        foreach (var (key, selector) in new[] { ("containerNumber", "ContainerNumberField"), ("sealNumber", "SealNumberField") })
            if (!draft["Rows"]!.AsArray().Any(r => ModelVerdict.S(r, "MapKind") == "Container" && ModelVerdict.S(r, "CanonicalKey") == key))
                Include(ModelVerdict.SourceFields(model, draft, "Container")[ModelVerdict.S(draft, selector)]?["confidence"]);
        return values.Count == 0 ? null : values.Average();
    }
}

public sealed record CheckedReview(JsonObject Review, JsonObject? Draft, string MappingPath, UpgradeVerdict Verdict);
public static class ModelCheck
{
    public static CheckedReview Read(string doc, string folder, string? mappingFile = null)
    {
        if (doc is not ("BL" or "MANIFEST" or "AUTO")) throw new ArgumentException("Document type must be BL, MANIFEST or AUTO.");
        var review = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "review.json")))!.AsObject();
        var active = review["ActiveSnapshot"] as JsonObject ?? throw new ArgumentException("Review has no active snapshot; run models draft again.");
        if (ModelVerdict.S(active, "DocumentTypeCode") != doc) throw new ArgumentException("Review document type mismatch.");
        var path = mappingFile ?? Path.Combine(folder, doc == "AUTO" ? "draft-classifier.json" : "draft-mapping.json");
        JsonObject? draft = null;
        UpgradeVerdict verdict;
        try
        {
            if (File.Exists(path)) draft = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new ArgumentException("Draft must be an object.");
            verdict = ModelVerdict.Evaluate(doc, active, draft, review, new());
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { verdict = new("RED", [new("RED", "Draft", "", "Correct final draft: " + e.Message)]); }
        return new(review, draft, path, verdict);
    }
    public static void Write(CheckedReview result, string folder, IProvisionConsole console)
    {
        console.WriteLine("Overall verdict: " + result.Verdict.Colour); console.WriteLine(result.Verdict.Table);
        File.WriteAllText(Path.Combine(folder, "verdict.txt"), "Overall verdict: " + result.Verdict.Colour + "\n" + result.Verdict.Table);
        File.WriteAllText(Path.Combine(folder, "verdict.json"), JsonSerializer.Serialize(result.Verdict, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static int Run(OperatorArguments args, IProvisionConsole console)
    {
        var folder = args.Required("review"); var result = Read(args.Required("document-type-code"), folder, args.Has("mapping-file") ? args.Get("mapping-file") : null);
        Write(result, folder, console); return result.Verdict.ExitCode;
    }
}
