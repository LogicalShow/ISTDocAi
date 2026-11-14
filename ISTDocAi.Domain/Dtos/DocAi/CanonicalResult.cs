using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
public sealed record CanonicalResult(string DocumentTypeCode, string? ShippingLineCode, string ModelId, int PageCount, ImmutableDictionary<string, ExtractedField> Fields, ImmutableArray<ContainerFields> Containers, ImmutableArray<FieldFlag> Flags)
{
    public string ContractVersion => "1.0";
    public int FlaggedFields => Flags.Select(f => f.Field).Distinct(StringComparer.Ordinal).Count();
}
