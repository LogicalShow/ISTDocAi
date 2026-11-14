using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;

public interface IDocumentAnalyzer
{
    Task<bool> CheckAccessAsync(CancellationToken ct);
    Task<SubmitPermit> ReserveSubmitAsync(CancellationToken ct);
    Task<SubmitResult> SubmitAsync(SubmitPermit permit, OperationKind kind, string modelId, ReadOnlyMemory<byte> document, CancellationToken ct);
    Task DeleteResultAsync(string operationLocation, CancellationToken ct);
    Task<PollResult> PollAsync(string operationLocation, CancellationToken ct);
}
