using Azure.Core;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace ISTDocAi.OperatorTool;

// Deliberately separate from Graph/ARM AzureRest. The caller owns the HTTP client.
public sealed class DocumentModelClient
{
    public const string Scope = "https://cognitiveservices.azure.com/.default";
    private readonly HttpClient http;
    private readonly TokenCredential credential;
    private readonly Uri origin;
    public DocumentModelClient(HttpClient http, TokenCredential credential, string endpoint)
    { this.http = http; this.credential = credential; origin = ValidateEndpoint(endpoint); }
    public static Uri ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !uri.IsDefaultPort || !uri.Host.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Endpoint must be an HTTPS origin with a custom .cognitiveservices.azure.com subdomain, without path, query or userinfo.");
        return uri;
    }
    public async Task<JsonObject> Fetch(string id, bool classifier, string docType = "", CancellationToken ct = default, bool allowExpired = false)
    {
        ModelSql.Identifier(id);
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([Scope]), ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, "/documentintelligence/" + (classifier ? "documentClassifiers/" : "documentModels/") + Uri.EscapeDataString(id) + "?api-version=2024-11-30"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ArgumentException("Model not found in this Document Intelligence resource.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ArgumentException("Your account needs the Cognitive Services User role on this Document Intelligence resource.");
            if (!response.IsSuccessStatusCode) throw new ActionNeededException("Azure is unreachable; upgrade refused.");
            var model = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)) as JsonObject ?? throw new ArgumentException("Invalid Azure model schema.");
            if (!allowExpired && model["expirationDateTime"] is JsonValue expiry && DateTimeOffset.TryParse(expiry.ToString(), out var date) && date <= DateTimeOffset.UtcNow) throw new ArgumentException("Model has expired; upgrade refused.");
            if (classifier) { if (model["docTypes"] is not JsonObject) throw new ArgumentException("Classifier has no classes."); }
            else
            {
                var types = model["docTypes"] as JsonObject ?? throw new ArgumentException("Model has no docTypes.");
                if (docType.Length == 0 && types.Count != 1) throw new ArgumentException("Several docTypes: --doc-type is required. Available: " + string.Join(", ", types.Select(x => x.Key)));
                var selected = docType.Length == 0 ? types.Single().Key : docType;
                if (types[selected] is not JsonObject type || type["fieldSchema"] is not JsonObject) throw new ArgumentException("Requested docType not found or has no field schema.");
                model["selectedDocType"] = selected;
                Normalize(type["fieldSchema"]!.AsObject(), type["fieldConfidence"] as JsonObject);
                if (model["buildMode"] is null && type["buildMode"] is not null) model["buildMode"] = type["buildMode"]!.DeepClone();
            }
            return model;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { throw new ActionNeededException("Azure is unreachable; upgrade refused."); }
    }
    private static void Normalize(JsonObject fields, JsonObject? confidence, string prefix = "")
    {
        foreach (var field in fields)
        {
            if (field.Value is not JsonObject schema) throw new ArgumentException("Invalid Azure field schema.");
            NormalizeNode(schema, confidence, prefix + field.Key);
        }
    }
    private static void NormalizeNode(JsonObject schema, JsonObject? confidence, string path)
    {
        var supplied = schema["confidence"] ?? confidence?[path];
        schema["confidence"] = supplied is JsonValue ? supplied.DeepClone() : JsonValue.Create("unavailable");
        if (schema["properties"] is JsonObject properties) Normalize(properties, confidence, path + ".");
        if (schema["items"] is JsonObject items) NormalizeNode(items, confidence, path + ".*");
    }
    public static JsonObject Fields(JsonObject model) => (JsonObject)model["docTypes"]![model["selectedDocType"]!.ToString()]!["fieldSchema"]!;
}
