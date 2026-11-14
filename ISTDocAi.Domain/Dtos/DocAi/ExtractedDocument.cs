using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
/// <summary>A null field entry means not found; a present field may itself have a null normalized value.</summary>
public sealed record ExtractedDocument(string DocumentTypeCode, string? ShippingLineCode, string ModelId, int PageCount, ImmutableDictionary<string, ExtractedField?> Fields, ImmutableArray<ContainerFields> Containers);
