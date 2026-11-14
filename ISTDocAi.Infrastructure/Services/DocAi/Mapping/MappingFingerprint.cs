using System.Security.Cryptography;
using System.Text.Json;

using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public static class MappingFingerprint
{
    // Bump for any output-affecting parser/mapper change, including ROW_MERGED and totals checks.
    public const string MappingAlgorithmVersion = "2";
    public const string CanonicalContractVersion = "1.0";

    // The caller has already resolved this snapshot; never select another profile while hashing it.
    public static string ComputeEffective(AnalyzeResultMappingOptions profile, string documentType,
        string algorithmVersion = MappingAlgorithmVersion)
    {
        // Exclude Models: unrelated profiles must not invalidate this model's result.
        var effective = new Dictionary<string, object?>
        {
            ["algorithmVersion"] = algorithmVersion, ["contractVersion"] = CanonicalContractVersion,
            ["documentType"] = documentType, ["preserveExtractedText"] = profile is AnalyzeResultMappingProfile,
            ["fieldMap"] = profile.FieldMap, ["containerFieldMap"] = profile.ContainerFieldMap,
            ["containersField"] = profile.ContainersField, ["containerNumberField"] = profile.ContainerNumberField,
            ["sealNumberField"] = profile.SealNumberField
        };
        if (profile is AnalyzeResultMappingProfile selected)
        {
            effective["headerFieldMap"] = selected.HeaderFieldMap;
            effective["billFieldMap"] = selected.BillFieldMap;
            effective["billsOfLadingField"] = selected.BillsOfLadingField;
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToElement(effective));
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
