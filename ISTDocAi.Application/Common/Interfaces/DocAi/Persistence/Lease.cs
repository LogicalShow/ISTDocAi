namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record Lease(Guid JobId, Guid LeaseToken);
