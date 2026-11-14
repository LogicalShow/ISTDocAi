using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class SqlClaimTestHelperTests
{
    [Test]
    public async Task Briefly_not_due_outbox_returns_the_first_nonempty_list_without_reclaiming()
    {
        IReadOnlyList<ClaimedDelivery> deliveries = [
            new(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, "job.failed", "{}", 0),
            new(2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, "job.completed", "{}", 0)];
        var calls = 0;
        var result = await SqlClaimTestHelper.ExpectedDeliveriesAsync(_ =>
            Task.FromResult<IReadOnlyList<ClaimedDelivery>>(++calls < 3 ? [] : deliveries));
        Assert.That(result, Is.SameAs(deliveries));
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public void Empty_outbox_fails_with_expected_delivery_claim_within_the_bound()
    {
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SqlClaimTestHelper.ExpectedDeliveriesAsync(_ =>
                Task.FromResult<IReadOnlyList<ClaimedDelivery>>([])).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(error!.Message, Is.EqualTo("Expected delivery claim."));
    }

    [Test]
    public void Outbox_caller_cancellation_is_not_replaced_by_expected_delivery_claim()
    {
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        Assert.That(async () => await SqlClaimTestHelper.ExpectedDeliveriesAsync(_ =>
            Task.FromResult<IReadOnlyList<ClaimedDelivery>>([]), stop.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Briefly_not_due_claim_is_retried_then_returns_the_same_job()
    {
        var job = new ClaimedJob(Guid.NewGuid(), 1, Guid.NewGuid(), 0, 1, "BL", null, "Extract", "test", 1, 0, "test");
        var calls = 0;
        var result = await SqlClaimTestHelper.ExpectedAsync(_ => Task.FromResult<ClaimedJob?>(++calls < 3 ? null : job));
        Assert.That(result, Is.SameAs(job));
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public void Empty_queue_fails_with_expected_claim_within_the_bound()
    {
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SqlClaimTestHelper.ExpectedAsync(_ => Task.FromResult<ClaimedJob?>(null)).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(error!.Message, Is.EqualTo("Expected claim."));
    }

    [Test]
    public void Caller_cancellation_is_not_replaced_by_expected_claim()
    {
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        Assert.That(async () => await SqlClaimTestHelper.ExpectedAsync(_ => Task.FromResult<ClaimedJob?>(null), stop.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
