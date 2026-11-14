using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
public sealed record ExtractedManifest(string? ShippingLineCode, string ModelId, int PageCount, ImmutableDictionary<string, ExtractedField> Header, ImmutableArray<ManifestRow> BillsOfLading, ImmutableArray<ManifestRow> Containers);
