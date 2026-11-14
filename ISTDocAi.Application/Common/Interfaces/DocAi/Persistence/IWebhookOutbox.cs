namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IWebhookOutbox
{
    Task<IReadOnlyList<ClaimedDelivery>> ClaimDueAsync(CancellationToken cancellationToken);
    Task<WriteOutcome> RecordAttemptAsync(long deliveryId, Guid leaseToken, short? statusCode, string? error, CancellationToken cancellationToken);
    Task<int> ReplayAsync(short consumerId, CancellationToken cancellationToken);
    Task<EventContext?> GetEventContextAsync(Guid jobId, short runNo, CancellationToken cancellationToken);
}
