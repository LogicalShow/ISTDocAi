using ISTDocAi.Domain.Dtos.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
public sealed record MappedResult(ISTDocAi.Application.Common.Contracts.DocAi.V1.ICanonicalResult Result, int FlaggedFields, string ResultType, string? MappingFingerprint = null);
