using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
public sealed record CanonicalManifestResult(string? ShippingLineCode, string ModelId, int PageCount, ImmutableDictionary<string, ExtractedField> Header, ImmutableArray<ManifestBill> BillsOfLading, ImmutableArray<UnidentifiedManifestBill> UnidentifiedBillsOfLading, ImmutableArray<ManifestContainer> UnassignedContainers, ImmutableArray<FieldFlag> Flags)
{
    public string ContractVersion => "1.0";
    public string DocumentTypeCode => "MANIFEST";
    public int FlaggedFields => Flags.Select(f => f.Field).Distinct(StringComparer.Ordinal).Count();
}
