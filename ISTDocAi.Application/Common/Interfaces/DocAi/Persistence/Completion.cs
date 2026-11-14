namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record Completion(Guid JobId, Guid LeaseToken, string ResultJson, short FlaggedFields, Guid EventId, string PayloadJson, string? ResultType = null, string? MappingFingerprint = null);
