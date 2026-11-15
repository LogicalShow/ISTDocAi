namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
public sealed record CancelJobResult(Guid JobId, short RunNo, string Outcome);
