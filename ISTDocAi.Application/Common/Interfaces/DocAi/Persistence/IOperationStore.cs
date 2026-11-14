namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IOperationStore
{
    Task<BeginOperationResult> BeginAsync(Lease lease, string kind, string modelId, CancellationToken cancellationToken);
    Task<bool> AcceptedAsync(long operationId, string operationLocation, CancellationToken cancellationToken);
    Task<WriteOutcome> CloseSendingAsync(long operationId, Guid leaseToken, string state, string? azureErrorCode, CancellationToken cancellationToken);
    Task<WriteOutcome> ExtractionSucceededAsync(ExtractionSuccess request, CancellationToken cancellationToken);
}
