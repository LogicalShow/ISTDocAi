using ISTDocAi.Application.Common.Interfaces.DocAi;
namespace ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;

/// <summary>One-way startup latch. Failure never releases runtime consumers.</summary>
public sealed class DatabaseReadiness : IDatabaseReadiness
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DatabaseReadinessStatus status = new(DatabaseReadinessState.Pending, "DATABASE_NOT_READY");
    public DatabaseReadinessStatus Status => Volatile.Read(ref status);
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => ready.Task.WaitAsync(cancellationToken);
    public void MarkReady()
    {
        var current = Status;
        if (current.State == DatabaseReadinessState.Pending &&
            ReferenceEquals(Interlocked.CompareExchange(ref status, new(DatabaseReadinessState.Ready, ""), current), current))
            ready.TrySetResult();
    }
    public void MarkFailed(string code)
    {
        var current = Status;
        if (current.State == DatabaseReadinessState.Pending)
            Interlocked.CompareExchange(ref status, new(DatabaseReadinessState.Failed, code), current);
    }
}
