using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.UI.Server.Tests;
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

internal sealed class Clock : TimeProvider
{
 public DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
 public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class BlobReady : ISTDocAi.Application.Common.Interfaces.DocAi.Storage.IDocumentStore
{
 public Task CheckAsync(CancellationToken ct) => Task.CompletedTask;
 public Task PutAsync(string path, Stream content, string contentType, CancellationToken ct) => throw new NotSupportedException();
 public Task<Stream> OpenReadAsync(string path, CancellationToken ct) => throw new NotSupportedException();
}
internal sealed class OneCallback : ICallbackDirectory
{
 public Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CallbackConsumer>>([new(1, "TEST", "https://test.example/callback", "secret", null)]);
}
