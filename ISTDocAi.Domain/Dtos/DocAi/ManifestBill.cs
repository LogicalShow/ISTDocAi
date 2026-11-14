using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
public sealed record ManifestBill(ImmutableDictionary<string, ExtractedField> Fields, ImmutableArray<ManifestContainer> Containers);
