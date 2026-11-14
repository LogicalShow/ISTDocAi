using System.Threading.Channels;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;

namespace ISTDocAi.Infrastructure.Services.DocAi.Webhooks;

// Consumer IDs are smallints: at most 65,536 throttle entries and queued notifications.
public sealed class ConsumerWebhookEvents(TimeProvider clock) : IConsumerWebhookEvents
{
    private readonly object gate = new();
    private readonly Dictionary<short, long> last = new();
    private readonly Channel<short> pending = Channel.CreateUnbounded<short>(new() { SingleReader = true });

    public void NotifyAlive(short consumerId)
    {
        lock (gate)
        {
            var now = clock.GetTimestamp();
            if (last.TryGetValue(consumerId, out var previous) &&
                clock.GetElapsedTime(previous, now) < TimeSpan.FromMinutes(1)) return;
            // Keep one notification per consumer queued even if the sender is disabled or not ready.
            if (!queued.Add(consumerId)) return;
            last[consumerId] = now;
            pending.Writer.TryWrite(consumerId);
        }
    }
    private readonly HashSet<short> queued = new();
    public bool TryRead(out short consumerId)
    {
        lock (gate)
        {
            if (!pending.Reader.TryRead(out consumerId)) return false;
            queued.Remove(consumerId);
            return true;
        }
    }
    public async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var signal = pending.Reader.WaitToReadAsync(wait.Token).AsTask();
        var timer = Task.Delay(delay, clock, wait.Token);
        try { await await Task.WhenAny(signal, timer); }
        finally { await wait.CancelAsync(); }
    }
}
