using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Collections.Immutable;
using System.Text.Json;


namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public static class ManifestAnalyzeResultParser
{
    public static ExtractedManifest Parse(string rawJson, string? shippingLineCode, string modelId, AnalyzeResultMappingProfile profile)
    {
        using var json = JsonDocument.Parse(rawJson);
        var root = json.RootElement;
        // Fixtures are Azure responses; persisted worker results contain analyzeResult itself.
        if (root.TryGetProperty("analyzeResult", out var result)) root = result;
        var pages = root.TryGetProperty("pages", out var p) ? p.GetArrayLength() : 0;
        var header = ImmutableDictionary.CreateBuilder<string, ExtractedField>(StringComparer.Ordinal);
        var bills = ImmutableArray.CreateBuilder<ManifestRow>();
        var containers = ImmutableArray.CreateBuilder<ManifestRow>();
        if (root.TryGetProperty("documents", out var docs) && docs.GetArrayLength() > 0 && docs[0].TryGetProperty("fields", out var fields))
        {
            foreach (var (key, name) in profile.HeaderFieldMap.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (fields.TryGetProperty(name, out var field) && ReadField(field, pages) is { } value) header.Add(key, value);
            ReadRows(profile.BillsOfLadingField, profile.BillFieldMap, bills);
            ReadRows(profile.ContainersField, profile.ContainerFieldMap, containers);

            void ReadRows(string name, Dictionary<string, string> map, ImmutableArray<ManifestRow>.Builder target)
            {
                if (!fields.TryGetProperty(name, out var array) || !array.TryGetProperty("valueArray", out var items)) return;
                var index = 0;
                foreach (var item in items.EnumerateArray())
                {
                    var values = ImmutableDictionary.CreateBuilder<string, ExtractedField>(StringComparer.Ordinal);
                    var geometry = ImmutableDictionary.CreateBuilder<string, ImmutableArray<CellRegion>>(StringComparer.Ordinal);
                    if (item.TryGetProperty("valueObject", out var obj))
                    {
                        // Include every source cell's geometry, even an empty or unmapped cell.
                        foreach (var cell in obj.EnumerateObject()) geometry.Add(cell.Name, ReadRegions(cell.Value));
                        foreach (var (key, model) in map.OrderBy(p => p.Key, StringComparer.Ordinal))
                            if (obj.TryGetProperty(model, out var field) && ReadField(field, pages) is { } value) {
                                values.Add(key, key == "containerNumber" ? value with { Value = MappingServices.Iso.Normalize(value.Value), RawText = value.RawText ?? (MappingServices.Iso.Normalize(value.Value) != value.Value ? value.Value : null) } : value);
                                geometry[key] = ReadRegions(field);
                            }
                    }
                    target.Add(new(index++, values.ToImmutable(), geometry.ToImmutable()));
                }
            }
        }
        return new(shippingLineCode, modelId, pages, header.ToImmutable(), bills.ToImmutable(), containers.ToImmutable());
    }

    private static ExtractedField? ReadField(JsonElement field, int pages) => AnalyzeResultParser.Field(field, pages, preserveExtractedText: true);

    private static ImmutableArray<CellRegion> ReadRegions(JsonElement field)
    {
        var result = ImmutableArray.CreateBuilder<CellRegion>();
        if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("boundingRegions", out var regions) || regions.ValueKind != JsonValueKind.Array) return [];
        foreach (var region in regions.EnumerateArray())
        {
            if (region.ValueKind != JsonValueKind.Object || !region.TryGetProperty("pageNumber", out var page) || page.ValueKind != JsonValueKind.Number || !page.TryGetInt32(out var number) ||
                !region.TryGetProperty("polygon", out var polygon) || polygon.ValueKind != JsonValueKind.Array) continue;
            var coordinates = ImmutableArray.CreateBuilder<double>();
            foreach (var coordinate in polygon.EnumerateArray())
                coordinates.Add(coordinate.ValueKind == JsonValueKind.Number && coordinate.TryGetDouble(out var v) ? v : double.NaN);
            result.Add(new(number, coordinates.ToImmutable()));
        }
        return result.ToImmutable();
    }
}
