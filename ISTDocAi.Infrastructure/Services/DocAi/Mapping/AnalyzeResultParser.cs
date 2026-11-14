using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;


namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public static class AnalyzeResultParser
{
    // Input is analyzeResult itself (as returned by PollResult.Succeeded / the raw-result store).
    public static ExtractedDocument Parse(string rawJson, string documentTypeCode, string? shippingLineCode,
        string modelId, AnalyzeResultMappingOptions? options = null)
    {
        options ??= new();
        ExtractedField? ReadField(JsonElement field, int pages) => Field(field, pages, options is AnalyzeResultMappingProfile);
        using var json = JsonDocument.Parse(rawJson);
        var root = json.RootElement;
        var pages = root.TryGetProperty("pages", out var p) ? p.GetArrayLength() : 0;
        var fields = ImmutableDictionary.CreateBuilder<string, ExtractedField?>(StringComparer.Ordinal);
        var containers = ImmutableArray.CreateBuilder<ContainerFields>();
        if (FirstDocument(root) is { } document && document.TryGetProperty("fields", out var source))
        {
            foreach (var (canonical, model) in options.FieldMap)
                if (source.TryGetProperty(model, out var field) && ReadField(field, pages) is { } parsed) fields.Add(canonical, parsed);
            if (source.TryGetProperty(options.ContainersField, out var array) && array.TryGetProperty("valueArray", out var items))
                foreach (var item in items.EnumerateArray())
                    if (item.TryGetProperty("valueObject", out var obj) && obj.TryGetProperty(ContainerName("containerNumber", options.ContainerNumberField), out var number)
                        && ReadField(number, pages) is { } parsed)
                    {
                        containers.Add(new(parsed with { Value = MappingServices.Iso.Normalize(parsed.Value), RawText = parsed.RawText ?? (MappingServices.Iso.Normalize(parsed.Value) != parsed.Value ? parsed.Value : null) }, obj.TryGetProperty(ContainerName("sealNumber", options.SealNumberField), out var seal) ? ReadField(seal, pages) : null,
                            Extra("containerType"), Extra("packages"), Extra("grossWeightKg"), Extra("measurementCbm")));
                        ExtractedField? Extra(string key) => options.ContainerFieldMap.TryGetValue(key, out var name) && obj.TryGetProperty(name, out var value) ? ReadField(value, pages) : null;
                    }
        }
        string ContainerName(string key, string fallback) => options.ContainerFieldMap.GetValueOrDefault(key, fallback);
        return new(documentTypeCode, shippingLineCode, modelId, pages, fields.ToImmutable(), containers.ToImmutable());
    }
    public static ClassifierResult ParseClassifier(string rawJson)
    {
        using var json = JsonDocument.Parse(rawJson);
        return FirstDocument(json.RootElement) is { } doc
            ? new(doc.TryGetProperty("docType", out var type) ? type.GetString() : null, Confidence(doc)) : new(null, 0);
    }
    private static JsonElement? FirstDocument(JsonElement root) =>
        root.TryGetProperty("documents", out var docs) && docs.GetArrayLength() > 0 ? docs[0] : null;
    private static double Confidence(JsonElement field) =>
        field.ValueKind == JsonValueKind.Object && field.TryGetProperty("confidence", out var c) &&
        c.ValueKind == JsonValueKind.Number && c.TryGetDouble(out var value) && double.IsFinite(value) && value is >= 0 and <= 1 ? value : 0;
    private static string? Text(JsonElement field, string name) =>
        field.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString()) ? text.GetString() : null;
    internal static ExtractedField? Field(JsonElement field, int pages, bool preserveExtractedText = false)
    {
        if (field.ValueKind != JsonValueKind.Object) return null;
        var content = Text(field, "content");
        string? value = null;
        bool normalizationFailed = false;
        foreach (var name in new[] { "valueString", "valueDate", "valueNumber", "valueInteger" })
        {
            if (!field.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) continue;
            if (name == "valueString")
            {
                if (v.ValueKind == JsonValueKind.String) value = Text(field, name);
                else normalizationFailed = true;
                // An empty string is not a value; continue to another representation or content.
                if (value is null && !normalizationFailed) continue;
            }
            else if (name == "valueDate")
            {
                if (v.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(v.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                else normalizationFailed = true;
            }
            else
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var number)) value = number.ToString(CultureInfo.InvariantCulture);
                else normalizationFailed = true;
            }
            break;
        }
        // Keep the original text when normalization fails; do not label it as a normalized value.
        if (!normalizationFailed) value ??= content;
        if (value is null && content is null) return null;
        int? page = null;
        if (field.TryGetProperty("boundingRegions", out var regions) && regions.ValueKind == JsonValueKind.Array && regions.GetArrayLength() > 0
            && regions[0].ValueKind == JsonValueKind.Object && regions[0].TryGetProperty("pageNumber", out var n)
            && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var pageNumber)) page = pageNumber;
        // Only found fields may fall back to page 1, and only when the file has pages.
        page ??= pages > 0 ? 1 : null;
        if (page is null) return null;
        if (preserveExtractedText)
        {
            // Prefer the model's string; otherwise preserve printed formatting for dates and numbers.
            value = Text(field, "valueString") ?? content ?? value;
        }
        return new(value, content != value ? content : null, Confidence(field), page.Value);
    }
}
