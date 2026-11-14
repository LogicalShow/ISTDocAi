using System.Text.Json;
using System.Text.Json.Serialization;

namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

[JsonConverter(typeof(CanonicalResultJsonConverter))]
public interface ICanonicalResult
{
    string ContractVersion { get; }
    string DocumentTypeCode { get; }
    string? ShippingLineCode { get; }
    string ModelId { get; }
    int PageCount { get; }
}

public static class ResultTypes
{
    public const string BillOfLading = "bill-of-lading.v1";
    public const string Manifest = "cargo-manifest.v1";
}

public static class CanonicalResultCodec
{
    public static string TypeOf(ICanonicalResult result) => result switch
    {
        CanonicalResult => ResultTypes.BillOfLading,
        CanonicalManifestResult => ResultTypes.Manifest,
        _ => throw new JsonException("Unsupported canonical result type.")
    };

    public static void ValidateType(string? resultType)
    {
        if (resultType is not (null or ResultTypes.BillOfLading or ResultTypes.Manifest))
            throw new JsonException("Unsupported canonical result discriminator.");
    }

    public static ICanonicalResult Deserialize(string json, string? resultType)
    {
        ValidateType(resultType);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("documentTypeCode", out var type) || type.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(type.GetString()) || type.GetString() == "AUTO")
            throw new JsonException("Missing or unresolved canonical document type.");
        // Historical explicit types used the original BL-shaped canonical contract.
        resultType ??= type.GetString() == "MANIFEST" ? ResultTypes.Manifest : ResultTypes.BillOfLading;
        if ((resultType == ResultTypes.Manifest) != (type.GetString() == "MANIFEST"))
            throw new JsonException("Canonical document type and discriminator disagree.");
        return resultType switch
        {
            ResultTypes.BillOfLading => JsonSerializer.Deserialize(json, ContractJsonContext.Default.CanonicalResult)
                ?? throw new JsonException("Missing canonical result."),
            ResultTypes.Manifest => JsonSerializer.Deserialize(json, ContractJsonContext.Default.CanonicalManifestResult)
                ?? throw new JsonException("Missing canonical result."),
            _ => throw new JsonException("Unsupported canonical result discriminator.")
        };
    }

    public static string Serialize(ICanonicalResult result) => result switch
    {
        CanonicalResult bill => JsonSerializer.Serialize(bill, ContractJsonContext.Default.CanonicalResult),
        CanonicalManifestResult manifest => JsonSerializer.Serialize(manifest, ContractJsonContext.Default.CanonicalManifestResult),
        _ => throw new JsonException("Unsupported canonical result type.")
    };
}

public sealed class CanonicalResultJsonConverter : JsonConverter<ICanonicalResult>
{
    public override ICanonicalResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        using var json = JsonDocument.ParseValue(ref reader);
        return CanonicalResultCodec.Deserialize(json.RootElement.GetRawText(), null);
    }
    public override void Write(Utf8JsonWriter writer, ICanonicalResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case CanonicalResult bill: JsonSerializer.Serialize(writer, bill, ContractJsonContext.Default.CanonicalResult); break;
            case CanonicalManifestResult manifest: JsonSerializer.Serialize(writer, manifest, ContractJsonContext.Default.CanonicalManifestResult); break;
            default: throw new JsonException("Unsupported canonical result type.");
        }
    }
}
