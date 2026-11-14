using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

// Test-only: datetime2(3) rounding can make a newly queued row briefly not due.
internal static class SqlClaimTestHelper
{
    internal static async Task<IReadOnlyList<ClaimedDelivery>> ExpectedDeliveriesAsync(
        Func<CancellationToken, Task<IReadOnlyList<ClaimedDelivery>>> claim, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            while (true)
            {
                var deliveries = await claim(timeout.Token).WaitAsync(timeout.Token);
                if (deliveries.Count > 0) return deliveries;
                await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new InvalidOperationException("Expected delivery claim.");
        }
    }

    internal static async Task<ClaimedJob> ExpectedAsync(
        Func<CancellationToken, Task<ClaimedJob?>> claim, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            while (true)
            {
                var job = await claim(timeout.Token).WaitAsync(timeout.Token);
                if (job is not null) return job;
                await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new InvalidOperationException("Expected claim.");
        }
    }
}
