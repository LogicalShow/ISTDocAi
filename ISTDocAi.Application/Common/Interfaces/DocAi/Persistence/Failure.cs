namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record Failure(Guid JobId, Guid LeaseToken, string ErrorCode, string? ErrorMessage, bool CountsAsAttempt,
    Guid EventId, string PayloadJson, long? OperationId = null, string? AzureErrorCode = null);
