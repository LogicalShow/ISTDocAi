using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class TestClock : TimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly List<Timer> timers = [];
    public List<TimeSpan> Delays { get; } = [];
    public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
    public override long GetTimestamp() => GetUtcNow().Ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            Delays.Add(dueTime);
            var timer = new Timer(this, callback, state);
            timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
    }
    public void Advance(TimeSpan duration)
    {
        List<Timer> due;
        lock (gate)
        {
            now += duration;
            due = timers.Where(t => !t.Disposed && t.Due <= now).ToList();
            foreach (var t in due) t.Due = t.Period > TimeSpan.Zero ? now + t.Period : DateTimeOffset.MaxValue;
        }
        foreach (var t in due) if (!t.Disposed) t.Callback(t.State);
    }
    private sealed class Timer(TestClock owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public DateTimeOffset Due { get; set; }
        public TimeSpan Period { get; set; }
        public bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner.now + dueTime;
                Period = period;
                return true;
            }
        }
        public void Dispose() { lock (owner.gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
