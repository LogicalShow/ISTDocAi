using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
public sealed record JobRunSnapshot(int RunNo, RunReason Reason, RunStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc = null, DomainError? Error = null)
{
}
