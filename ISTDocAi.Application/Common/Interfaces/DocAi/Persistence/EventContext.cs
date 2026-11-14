namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record EventContext(string CorrelationId, string? ResultJson);
