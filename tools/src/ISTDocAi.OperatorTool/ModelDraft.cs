using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ISTDocAi.OperatorTool;

public sealed record DraftResult(JsonObject? Draft, JsonObject Review);
public static class ModelDraft
{
    private static string S(JsonNode node, string key) => node[key]?.ToString() ?? "";
    public static string Confidence(JsonNode? node) => node?["confidence"]?.ToString() ?? "unavailable";
    private static bool Compatible(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null || S(a, "type") != S(b, "type")) return false;
        if (S(a, "type") == "array") return Compatible(a["items"], b["items"]);
        if (S(a, "type") != "object") return S(a, "type").Length > 0;
        var ap = a["properties"] as JsonObject; var bp = b["properties"] as JsonObject;
        return ap is not null && bp is not null && ap.Count == bp.Count && ap.All(p => Compatible(p.Value, bp[p.Key]));
    }
    // AzureName is a literal property name relative to its MapKind, never a dotted traversal.
    public static DraftResult Extract(JsonObject snapshot, string token, JsonObject oldFields, JsonObject newFields, string modelId, string reference, Func<string, string[], string?> choose)
    {
        var profile = snapshot["MappingProfile"] as JsonObject ?? throw new ArgumentException("Active model has no mapping profile.");
        var doc = S(snapshot, "DocumentTypeCode"); var rows = profile["MappingFields"]!.AsArray();
        var events = new JsonArray(); var kept = new JsonArray(); var blocked = false; var unresolved = false;
        void Event(string status, string kind, string name, string key, JsonNode? field, string reason = "") => events.Add(new JsonObject { ["Status"] = status, ["MapKind"] = kind, ["AzureName"] = name, ["CanonicalKey"] = key, ["Confidence"] = Confidence(field), ["Reason"] = reason });
        JsonObject Scope(JsonObject fields, string kind) => kind switch
        {
            "Container" => fields[S(profile, "ContainersField")]?["items"]?["properties"] as JsonObject ?? new(),
            "Bill" => fields[S(profile, "BillsOfLadingField")]?["items"]?["properties"] as JsonObject ?? new(),
            _ => fields
        };
        void Selector(string name)
        {
            var field = newFields[name];
            if (field?["type"]?.ToString() != "array" || field["items"]?["type"]?.ToString() != "object" || field["items"]?["properties"] is not JsonObject)
            { blocked = true; Event("blocked", "Selector", name, "", field, "Expected array of objects."); }
        }
        Selector(S(profile, "ContainersField"));
        if (doc == "MANIFEST") Selector(S(profile, "BillsOfLadingField"));
        var container = Scope(newFields, "Container");
        foreach (var (selector, canonical) in new[] { ("ContainerNumberField", "containerNumber"), ("SealNumberField", "sealNumber") })
        {
            var name = rows.FirstOrDefault(r => S(r!, "MapKind") == "Container" && S(r!, "CanonicalKey") == canonical)?["AzureName"]?.ToString() ?? S(profile, selector);
            if (container[name] is not JsonObject field || S(field, "type") is "array" or "object" or "") { blocked = true; Event("blocked", "Selector", name, canonical, container[name], "Expected scalar container item field."); }
        }
        foreach (var row in rows)
        {
            var kind = S(row!, "MapKind"); var name = S(row!, "AzureName"); var key = S(row!, "CanonicalKey");
            var before = Scope(oldFields, kind)[name]; var after = Scope(newFields, kind)[name];
            if (Compatible(before, after)) { kept.Add(row!.DeepClone()); Event("carried", kind, name, key, after); }
            else Event("removed", kind, name, key, after, after is null ? "Path absent; canonical key no longer receives data." : "Type changed; removed + new.");
        }
        string[] kinds = doc == "MANIFEST" ? ["Header", "Field", "Bill", "Container"] : ["Field", "Container"];
        foreach (var kind in kinds)
        foreach (var field in Scope(newFields, kind))
        {
            if (doc == "MANIFEST" && kind == "Field" && !rows.Any(r => S(r!, "MapKind") == "Field" && S(r!, "AzureName") == field.Key)) continue;
            if (doc == "MANIFEST" && kind == "Header" && rows.Any(r => S(r!, "MapKind") == "Field" && S(r!, "AzureName") == field.Key) && !rows.Any(r => S(r!, "MapKind") == "Header" && S(r!, "AzureName") == field.Key)) continue;
            if (kind is "Field" or "Header" && (field.Key == S(profile, "ContainersField") || field.Key == S(profile, "BillsOfLadingField"))) continue;
            if (kept.Any(r => S(r!, "MapKind") == kind && S(r!, "AzureName") == field.Key)) continue;
            var options = rows.Where(r => S(r!, "MapKind") == kind).Select(r => S(r!, "CanonicalKey")).Distinct(StringComparer.Ordinal).ToArray();
            var answer = choose(kind + ":" + field.Key, options);
            if (answer is null || answer.Length == 0) { unresolved = true; Event("new", kind, field.Key, "", field.Value, "Unresolved."); continue; }
            if (answer == "ignore") { Event("ignored", kind, field.Key, "", field.Value); continue; }
            if (!Regex.IsMatch(answer, @"\A[A-Za-z][A-Za-z0-9_]{0,63}\z") || kept.Any(r => S(r!, "MapKind") == kind && S(r!, "CanonicalKey") == answer))
            { unresolved = true; Event("new", kind, field.Key, answer, field.Value, "Invalid or duplicate canonical key."); continue; }
            kept.Add(new JsonObject { ["MapKind"] = kind, ["CanonicalKey"] = answer, ["AzureName"] = field.Key }); Event("new", kind, field.Key, answer, field.Value, "Mapped by operator.");
        }
        // A removed/ignored override exposes the profile fallback; validate that effective selector too.
        foreach (var (selector, canonical) in new[] { ("ContainerNumberField", "containerNumber"), ("SealNumberField", "sealNumber") })
        {
            var name = kept.FirstOrDefault(r => S(r!, "MapKind") == "Container" && S(r!, "CanonicalKey") == canonical)?["AzureName"]?.ToString() ?? S(profile, selector);
            if (!blocked && (container[name] is not JsonObject field || S(field, "type") is "array" or "object" or ""))
            { blocked = true; Event("blocked", "Selector", name, canonical, container[name], "Final effective selector is missing or not scalar."); }
        }
        if (reference.Length is < 1 or > 256 || reference.TrimEnd(' ').Length == 0) { unresolved = true; Event("blocked", "ChangeReference", "", "", null, "Required: 1-256 characters, not only spaces."); }
        var draft = new JsonObject { ["DocumentTypeCode"] = doc, ["MappingShape"] = S(profile, "MappingShape"), ["ChangeReference"] = reference, ["Rows"] = kept };
        foreach (var name in new[] { "ContainersField", "ContainerNumberField", "SealNumberField", "BillsOfLadingField" }) draft[name] = profile[name]!.DeepClone();
        if (!blocked && !unresolved) ModelSql.Binding(modelId, doc, draft.ToJsonString());
        return new(blocked || unresolved ? null : draft, Review(snapshot, token, modelId, events));
    }
    private static JsonObject Review(JsonObject snapshot, string token, string model, JsonArray events) => new() { ["OldModelId"] = snapshot["Model"]?["ModelId"]?.DeepClone(), ["NewModelId"] = model, ["SNAPSHOT_TOKEN"] = token, ["Items"] = events };
    public static DraftResult Classifier(JsonObject snapshot, string token, JsonObject classes, string model, Func<string, string[], string?> choose, string? shippingLine = null)
    {
        var routes = new JsonObject(); var events = new JsonArray(); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var unresolved = false;
        foreach (var target in new[] { "BL", "MANIFEST", "" })
        {
            var old = snapshot["ClassifierClasses"]!.AsArray().SingleOrDefault(r => S(r!, "TargetDocumentTypeCode") == target);
            var name = old?["ClassName"]?.ToString() ?? "";
            var carried = classes.ContainsKey(name);
            var selected = carried ? name : choose(target.Length == 0 ? "other" : target, classes.Select(p => p.Key).ToArray());
            if (selected is null || !classes.ContainsKey(selected) || !used.Add(selected)) unresolved = true;
            else { ModelSql.Identifier(selected); routes[target == "BL" ? "bill-class-name" : target == "MANIFEST" ? "manifest-class-name" : "other-class-name"] = selected; }
            events.Add(new JsonObject { ["Status"] = carried ? "carried" : "new", ["OldClass"] = name, ["Class"] = selected, ["Target"] = target, ["Confidence"] = "unavailable" });
            if (!carried) events.Add(new JsonObject { ["Status"] = "removed", ["Class"] = name, ["Target"] = target });
            if (target == "BL") routes["bill-shipping-line"] = shippingLine ?? old?["ShippingLineCode"]?.ToString() ?? "";
        }
        foreach (var name in classes.Select(p => p.Key).Where(n => !used.Contains(n))) events.Add(new JsonObject { ["Status"] = "ignored", ["Class"] = name, ["Confidence"] = "unavailable" });
        var line = routes["bill-shipping-line"]!.ToString(); if (line.Length > 0) ModelSql.Identifier(line, 20);
        return new(unresolved ? null : routes, Review(snapshot, token, model, events));
    }
}
