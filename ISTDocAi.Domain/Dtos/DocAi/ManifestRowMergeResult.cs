using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Domain.Dtos.DocAi;
public sealed record ManifestRowMergeResult(ImmutableArray<ManifestRow> Rows, ImmutableDictionary<int, ImmutableHashSet<string>> ChangedFields);
