using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public static class MappingProfileHydrator
{
    public static AnalyzeResultMappingOptions Hydrate(MappingProfileHeader header, IEnumerable<MappingFieldRow> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);
        AnalyzeResultMappingOptions result = header.MappingShape switch
        {
            MappingShape.LegacyFallback when header.DocumentTypeCode == "BL" => new AnalyzeResultMappingOptions(),
            MappingShape.ModelProfile => new AnalyzeResultMappingProfile
            { DocumentTypeCode = header.DocumentTypeCode, BillsOfLadingField = header.BillsOfLadingField },
            _ => throw new ArgumentException("Invalid mapping shape or fallback document type.", nameof(header))
        };
        result.ContainersField = header.ContainersField;
        result.ContainerNumberField = header.ContainerNumberField;
        result.SealNumberField = header.SealNumberField;
        // Do not retain constructor defaults: an empty effective map must remain empty.
        result.FieldMap = new(StringComparer.Ordinal);
        result.ContainerFieldMap = new(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            var map = row.MapKind switch
            {
                MappingMapKind.Field => result.FieldMap,
                MappingMapKind.Container => result.ContainerFieldMap,
                MappingMapKind.Header when result is AnalyzeResultMappingProfile profile => profile.HeaderFieldMap,
                MappingMapKind.Bill when result is AnalyzeResultMappingProfile profile => profile.BillFieldMap,
                _ => throw new ArgumentException("Map kind is invalid for this mapping shape.", nameof(rows))
            };
            if (row.AzureName is null || !map.TryAdd(row.CanonicalKey, row.AzureName))
                throw new ArgumentException("Mapping rows contain a null source or duplicate key.", nameof(rows));
        }
        return result;
    }
}
