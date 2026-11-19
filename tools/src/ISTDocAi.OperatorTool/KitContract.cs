using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ISTDocAi.OperatorTool;

public sealed record ContractScope(bool Open, string[] Keys)
{
    public bool Accepts(string key) => Regex.IsMatch(key, @"\A[A-Za-z][A-Za-z0-9_]{0,63}\z") && (Open || Keys.Contains(key, StringComparer.Ordinal));
}

public sealed class KitContract
{
    public JsonObject Kit { get; }
    private readonly JsonObject bl;
    private readonly JsonObject manifest;
    public KitContract() : this(Resource("KitFields.json"), Resource("Schema.BL.json"), Resource("Schema.MANIFEST.json")) { }
    public KitContract(JsonObject kit, JsonObject blSchema, JsonObject manifestSchema) { Kit = kit; bl = blSchema; manifest = manifestSchema; }
    private static JsonObject Resource(string name)
    {
        using var stream = typeof(KitContract).Assembly.GetManifestResourceStream("ISTDocAi.OperatorTool." + name) ?? throw new InvalidOperationException("Missing contract resource: " + name);
        return JsonNode.Parse(stream)!.AsObject();
    }
    public static string ResultType(string doc) => doc switch { "BL" => "bill-of-lading.v1", "MANIFEST" => "cargo-manifest.v1", _ => throw new ArgumentException("Extract type must be BL or MANIFEST.") };
    public ContractScope Scope(string doc, string kind)
    {
        var root = doc == "BL" ? bl : doc == "MANIFEST" ? manifest : throw new ArgumentException("Unknown document type.");
        JsonNode props = root["properties"]!;
        JsonNode? scope = (doc, kind) switch
        {
            ("BL", "Field") => props["fields"],
            ("BL", "Container") => props["containers"]?["items"],
            ("MANIFEST", "Header" or "Field") => props["header"],
            ("MANIFEST", "Bill") => props["billsOfLading"]?["items"]?["properties"]?["fields"],
            ("MANIFEST", "Container") => props["billsOfLading"]?["items"]?["properties"]?["containers"]?["items"],
            _ => null
        };
        if (scope is null) return new(false, []);
        while (scope["$ref"] is JsonValue reference)
        {
            var path = reference.ToString();
            if (!path.StartsWith("#/", StringComparison.Ordinal)) throw new InvalidOperationException("External schema reference unsupported.");
            scope = root;
            foreach (var part in path[2..].Split('/')) scope = scope![part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
        }
        // Open only when the schema says so explicitly (additionalProperties is a schema or true), or when it declares
        // no properties at all. An object with declared properties and no additionalProperties is FIXED: the product
        // DTOs behind it are fixed records (e.g. BL container items = ContainerFields, 6 properties), so an extra key
        // would be dropped by the service even though JSON Schema's own default would allow it.
        var additional = scope!["additionalProperties"];
        var open = additional is JsonObject
            || (additional is JsonValue v && v.TryGetValue<bool>(out var allowed) && allowed)
            || (additional is null && scope["properties"] is null);
        return new(open, (scope["properties"] as JsonObject)?.Select(p => p.Key).ToArray() ?? []);
    }
    public bool Consumes(string doc, string kind, string key) => Kit["resultTypes"]![ResultType(doc)]?[doc == "MANIFEST" && kind == "Field" ? "Header" : kind] is JsonArray keys && keys.Any(k => k!.ToString() == key);
    public string Table()
    {
        var lines = new List<string> { "| Result type | Scope (MapKind) | Canonical keys consumed by kit |", "| --- | --- | --- |" };
        foreach (var type in Kit["resultTypes"]!.AsObject())
        foreach (var scope in type.Value!.AsObject()) lines.Add($"| {type.Key} | {scope.Key} | {string.Join(", ", scope.Value!.AsArray().Select(k => "`" + k + "`"))} |");
        return string.Join("\n", lines);
    }
}
