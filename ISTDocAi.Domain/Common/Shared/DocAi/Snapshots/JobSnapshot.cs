using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
/// <summary>Data snapshot. Lifecycle rules live in Application services.</summary>
public sealed record JobSnapshot
{
    public JobStatus Status { get; init; }
    public JobStage Stage { get; init; }
    public JobRunSnapshot CurrentRun { get; init; } = null!;
    public string? RequestedShippingLineCode { get; init; }
    public string? ShippingLineCode { get; init; }
    public string? ModelId { get; init; }
    public int Attempts { get; init; }
    public int TakeoverCount { get; init; }
    public Guid? LeaseToken { get; init; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DomainError? Error { get; init; }
    public ImmutableArray<AzureOperationSnapshot> Operations { get; init; } = [];
}
