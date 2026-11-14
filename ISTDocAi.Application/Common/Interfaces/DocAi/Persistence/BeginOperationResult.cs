namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record BeginOperationResult(WriteOutcome Outcome, long? OperationId);
