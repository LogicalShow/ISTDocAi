using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class LeaseHeartbeatTests
{
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Long_POST_keeps_renewing_even_while_host_is_draining(bool shutdown)
    {
        var f = new WorkerFakes();
        using var stop = new CancellationTokenSource();
        var response = new TaskCompletionSource<SubmitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken postToken = default;
        f.SubmitHandler = ct => { postToken = ct; return response.Task; };
        var renewals = 0;
        f.RenewHandler = _ => { Interlocked.Increment(ref renewals); return Task.FromResult(WriteOutcome.Applied); };
        var task = f.Processor().ExecuteAsync(f.Job, stop.Token);
        if (shutdown) stop.Cancel();
        for (var i = 1; i <= 6; i++)
        {
            f.Clock.Advance(TimeSpan.FromSeconds(15));
            await Until(() => Volatile.Read(ref renewals) == i);
            Assert.That(postToken.IsCancellationRequested, Is.False);
            Assert.That(f.Released, Is.Null);
        }
        response.SetResult(new SubmitResult.Accepted("https://test/operations/1", null));
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(f.Calls.IndexOf("accepted"), Is.GreaterThan(f.Calls.IndexOf("post:bl-v1")));
        if (shutdown)
        {
            Assert.That(f.Calls.Last(), Is.EqualTo("release"));
            Assert.That(f.Calls, Does.Not.Contain("poll"));
        }
        else Assert.That(f.Completion, Is.Not.Null);
        var count = renewals;
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.That(renewals, Is.EqualTo(count), "heartbeat must be disposed with the job");
    }

    [Test]
    public async Task Long_RetryAfter_keeps_ownership_without_extra_poll_renewals()
    {
        var f = new WorkerFakes(); f.Accepted();
        f.Polls.Enqueue(new PollResult.Running(TimeSpan.FromSeconds(90)));
        var count = 0;
        f.RenewHandler = _ => { Interlocked.Increment(ref count); return Task.FromResult(WriteOutcome.Applied); };
        var task = f.Processor().ExecuteAsync(f.Job, default);
        for (var i = 1; i <= 5; i++)
        {
            f.Clock.Advance(TimeSpan.FromSeconds(15));
            await Until(() => Volatile.Read(ref count) == i);
            Assert.That(f.Calls.Count(c => c == "poll"), Is.EqualTo(1));
        }
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(f.Completion, Is.Not.Null);
        Assert.That(count, Is.InRange(5, 6));
    }

    [TestCase("lost")]
    [TestCase("database")]
    [TestCase("timeout")]
    public async Task Unconfirmed_renewal_cancels_POST_and_prevents_all_following_writes(string failure)
    {
        var f = new WorkerFakes();
        f.SubmitHandler = async ct => { await Task.Delay(Timeout.Infinite, ct); return new SubmitResult.NoAnswer(); };
        f.RenewHandler = async ct =>
        {
            if (failure == "database") throw new IOException("PRIVATE_TOKEN");
            if (failure == "timeout") await Task.Delay(Timeout.Infinite, ct);
            return WriteOutcome.LeaseLost;
        };
        var task = f.Processor().ExecuteAsync(f.Job, default);
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.That(f.Calls.Last(), Is.EqualTo("post:bl-v1"));
        Assert.That(f.Released, Is.Null);
        Assert.That(f.Completion, Is.Null);
        Assert.That(f.RetrySeconds, Is.Null);
        Assert.That(f.Logger.Lines.All(l => !l.Text.Contains("PRIVATE_TOKEN")), Is.True);
    }

    [Test]
    public async Task Lease_loss_interrupts_wait_before_another_poll()
    {
        var f = new WorkerFakes(); f.Accepted();
        f.Polls.Enqueue(new PollResult.Running(TimeSpan.FromMinutes(2)));
        f.Outcomes["renew"] = WriteOutcome.LeaseLost;
        var task = f.Processor().ExecuteAsync(f.Job, default);
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(f.Calls.Last(), Is.EqualTo("renew"));
        Assert.That(f.Calls.Count(c => c == "poll"), Is.EqualTo(1));
        Assert.That(f.Released, Is.Null);
    }

    [Test]
    public async Task Shutdown_during_location_persistence_keeps_heartbeat_until_durable_then_releases()
    {
        var f = new WorkerFakes();
        using var stop = new CancellationTokenSource();
        var durable = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken locationToken = default;
        f.AcceptedHandler = ct => { locationToken = ct; return durable.Task; };
        var task = f.Processor().ExecuteAsync(f.Job, stop.Token);
        stop.Cancel();
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await Until(() => f.Calls.Contains("renew"));
        Assert.That(locationToken.IsCancellationRequested, Is.False);
        Assert.That(f.Released, Is.Null);
        durable.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(f.Calls.Last(), Is.EqualTo("release"));
        Assert.That(f.Calls, Does.Not.Contain("poll"));
    }

    [Test]
    public async Task Shutdown_before_work_releases_without_POST()
    {
        var f = new WorkerFakes();
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await f.Processor().ExecuteAsync(f.Job, stop.Token);
        Assert.That(f.Calls, Does.Not.Contain("begin"));
        Assert.That(f.Calls.Last(), Is.EqualTo("release"));
    }

    [Test]
    public async Task Shutdown_immediately_after_202_persistence_releases_without_polling()
    {
        var f = new WorkerFakes();
        using var stop = new CancellationTokenSource();
        f.AfterAccepted = stop.Cancel;
        await f.Processor().ExecuteAsync(f.Job, stop.Token);
        Assert.That(f.Calls.Last(), Is.EqualTo("release"));
        Assert.That(f.Calls, Does.Contain("accepted").And.Not.Contain("poll"));
    }

    [Test]
    public async Task Independent_heartbeat_continues_during_synchronous_mapping()
    {
        var f = new WorkerFakes();
        using var mapping = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var count = 0;
        f.RenewHandler = _ => { Interlocked.Increment(ref count); return Task.FromResult(WriteOutcome.Applied); };
        f.OnParse = () => { mapping.Set(); if (!finish.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); };
        var task = Task.Run(() => f.Processor().ExecuteAsync(f.Job, default));
        try
        {
            await Until(() => mapping.IsSet);
            for (var i = 1; i <= 5; i++)
            {
                f.Clock.Advance(TimeSpan.FromSeconds(15));
                await Until(() => Volatile.Read(ref count) == i);
            }
            Assert.That(f.Completion, Is.Null);
        }
        finally { finish.Set(); }
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(f.Completion, Is.Not.Null);
    }

    [Test]
    public async Task Mapping_remains_owned_and_loss_prevents_completion()
    {
        var f = new WorkerFakes();
        f.Outcomes["renew"] = WriteOutcome.LeaseLost;
        f.OnParse = () =>
        {
            f.Clock.Advance(TimeSpan.FromSeconds(15));
            SpinWait.SpinUntil(() => f.Calls.Contains("renew"), TimeSpan.FromSeconds(5));
        };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.That(f.Completion, Is.Null);
        Assert.That(f.Calls, Does.Not.Contain("complete"));
        Assert.That(f.Released, Is.Null);
    }
}
