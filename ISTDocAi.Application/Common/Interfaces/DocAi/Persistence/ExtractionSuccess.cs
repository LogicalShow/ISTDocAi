namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record ExtractionSuccess(Guid JobId, Guid LeaseToken, long OperationId, string ModelId, int Pages,
    DateTimeOffset AzureCompletedAtUtc, string RawResultPath);
