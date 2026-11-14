using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
// Only the original fake members used by this stage; orchestration remains deferred.
internal sealed class WorkerFakes : IDocumentAnalyzer
{
 public Func<CancellationToken, Task<bool>>? AccessHandler { get; set; }
 public int AccessChecks;
 public Task<bool> CheckAccessAsync(CancellationToken ct)
 {
  Interlocked.Increment(ref AccessChecks);
  return AccessHandler?.Invoke(ct) ?? Task.FromResult(true);
 }
 public Task<SubmitPermit> ReserveSubmitAsync(CancellationToken ct) => throw new NotSupportedException();
 public Task<SubmitResult> SubmitAsync(SubmitPermit permit, OperationKind kind, string modelId, ReadOnlyMemory<byte> document, CancellationToken ct) => throw new NotSupportedException();
 public Task DeleteResultAsync(string operationLocation, CancellationToken ct) => throw new NotSupportedException();
 public Task<PollResult> PollAsync(string operationLocation, CancellationToken ct) => throw new NotSupportedException();
}
