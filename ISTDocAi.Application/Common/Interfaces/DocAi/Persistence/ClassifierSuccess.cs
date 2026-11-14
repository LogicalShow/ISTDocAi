namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record ClassifierSuccess(Guid JobId, Guid LeaseToken, long OperationId, string ClassifierModelId,
    int Pages, DateTimeOffset AzureCompletedAtUtc, string? ClassName, decimal? ClassConfidence, string? ResolvedLine,
    string? ErrorMessage, Guid EventId, string PayloadJson, string? ResolvedDocumentTypeCode = null);
