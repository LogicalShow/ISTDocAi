using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
/// <summary>Data snapshot. Lifecycle rules live in Application services.</summary>
public sealed record AzureOperationSnapshot
{
    public long OperationId { get; init; }
    public int RunNo { get; init; }
    public OperationKind Kind { get; init; }
    public string ModelId { get; init; } = "";
    public AzureOperationState State { get; init; }
    public string? OperationLocation { get; init; }
    public string? RawResultPath { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? AcceptedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
