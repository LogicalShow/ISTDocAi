using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
/// <summary>Data snapshot. Lifecycle rules live in Application services.</summary>
public sealed record WebhookDeliverySnapshot
{
    public int RunNo { get; init; }
    public WebhookDeliveryStatus Status { get; init; }
    public int Attempts { get; init; }
    public Guid? LeaseToken { get; init; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; init; }
    public DateTimeOffset? DeliveredAtUtc { get; init; }
    public int? LastStatusCode { get; init; }
    public string? LastError { get; init; }
}
