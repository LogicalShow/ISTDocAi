using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;



using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class WebhookSenderTests
{
    private sealed class Retry : IConsumerWebhookRetry
    {
        public readonly List<short> Consumers = [];
        public bool Throw;
        public Task<int> MakeDueAsync(short consumerId, CancellationToken ct)
        {
            Consumers.Add(consumerId);
            if (Throw) throw new InvalidOperationException("test");
            return Task.FromResult(1);
        }
    }

    [Test]
    public async Task Alive_wakes_idle_sender_throttles_per_consumer_and_errors_do_not_stop_claims()
    {
        var f = new WebhookFakes(); var events = new ConsumerWebhookEvents(f.Clock);
        var retry = new Retry();
        var services = new ServiceCollection();
        services.AddSingleton<IWebhookOutbox>(f); services.AddSingleton<IConsumerWebhookRetry>(retry);
        using var provider = services.BuildServiceProvider();
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions()), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance, events);
        await worker.StartAsync(default);
        await Until(() => f.Clock.Delays.Count > 0);
        events.NotifyAlive(1);
        await Until(() => Volatile.Read(ref f.Claims) >= 2);
        Assert.Equal(new short[] { 1 }, retry.Consumers);
        events.NotifyAlive(1);
        events.NotifyAlive(2);
        await Until(() => Volatile.Read(ref f.Claims) >= 3);
        Assert.Equal(new short[] { 1, 2 }, retry.Consumers);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        retry.Throw = true;
        events.NotifyAlive(1);
        await Until(() => retry.Consumers.Count == 3);
        var claims = f.Claims;
        events.NotifyAlive(3);
        await Until(() => f.Claims > claims);
        await worker.StopAsync(default);
        Assert.Equal(new short[] { 1, 2, 1, 3 }, retry.Consumers);
    }

    [Test]
    public async Task Concurrent_alive_notifications_are_coalesced_and_queue_survives_an_idle_boundary()
    {
        var f = new WebhookFakes(); var events = new ConsumerWebhookEvents(f.Clock);
        Parallel.For(0, 100, _ => events.NotifyAlive(1));
        await events.WaitAsync(TimeSpan.FromSeconds(30), default).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(events.TryRead(out var consumer)); Assert.Equal((short)1, consumer);
        Assert.False(events.TryRead(out _));
        f.Clock.Advance(TimeSpan.FromSeconds(59)); events.NotifyAlive(1);
        Assert.False(events.TryRead(out _));
        f.Clock.Advance(TimeSpan.FromSeconds(1)); events.NotifyAlive(1);
        Assert.True(events.TryRead(out _));
    }

    [Test]
    public async Task Shutdown_finishes_started_sends_records_them_and_leaves_queued_rows_to_the_lease()
    {
        var f = new WebhookFakes();
        var started = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Claim = _ => Task.FromResult<IReadOnlyList<ClaimedDelivery>>(Enumerable.Range(0, 10).Select(i => f.Delivery with { DeliveryId = i }).ToArray());
        f.Send = async (_, ct) => { Interlocked.Increment(ref started); await release.Task.WaitAsync(ct); return new(200); };
        using var provider = Services(f);
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions { MaxParallelSends = 4 }), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default); await Until(() => Volatile.Read(ref started) == 4);
        Assert.Equal(1, f.Claims); Assert.Equal(4, f.Requests.Count);
        var stopped = worker.StopAsync(default);
        await Task.Delay(30); Assert.False(stopped.IsCompleted);
        release.SetResult(); await stopped.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, f.Attempts.Count); Assert.False(f.RecordWasCancelled);
        Assert.Equal(1, f.Claims); Assert.Equal(4, f.Requests.Count);
    }
    [Test]
    public async Task Batch_drains_at_configured_parallelism_then_claims_again_immediately()
    {
        var f = new WebhookFakes(); var active = 0; var maximum = 0;
        using var permits = new SemaphoreSlim(0);
        f.Claim = _ => Task.FromResult<IReadOnlyList<ClaimedDelivery>>(f.Claims == 1 ? Enumerable.Range(0, 10).Select(i => f.Delivery with { DeliveryId = i }).ToArray() : []);
        f.Send = async (_, ct) =>
        {
            var count = Interlocked.Increment(ref active);
            int old; do { old = maximum; if (old >= count) break; } while (Interlocked.CompareExchange(ref maximum, count, old) != old);
            try { await permits.WaitAsync(ct); return new(200); } finally { Interlocked.Decrement(ref active); }
        };
        using var provider = Services(f);
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions { MaxParallelSends = 2 }), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default); await Until(() => Volatile.Read(ref active) == 2);
        Assert.Equal(2, maximum); permits.Release(10);
        await Until(() => f.Claims == 2); Assert.Equal(10, f.Attempts.Count); Assert.Equal(2, maximum);
        await worker.StopAsync(default);
    }
    
    [TestCase(60, 0, 4)]
    [TestCase(210, 0, 2)] // Exactly 90 seconds left still admits the next delivery.
    [TestCase(211, 0, 1)]
    [TestCase(60, 211, 0)] // SQL claim latency also consumes the lease.
    public async Task Single_slot_leaves_late_rows_unsent_until_their_lease_expires(int sendSeconds, int claimSeconds, int expectedSends)
    {
        var f = new WebhookFakes();
        f.Claim = _ =>
        {
            if (f.Claims != 1) return Task.FromResult<IReadOnlyList<ClaimedDelivery>>([]);
            f.Clock.Advance(TimeSpan.FromSeconds(claimSeconds));
            return Task.FromResult<IReadOnlyList<ClaimedDelivery>>(Enumerable.Range(0, 10)
                .Select(i => f.Delivery with { DeliveryId = i }).ToArray());
        };
        f.Send = (_, _) => { f.Clock.Advance(TimeSpan.FromSeconds(sendSeconds)); return Task.FromResult(new WebhookResponse(200)); };
        using var provider = Services(f);
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions { MaxParallelSends = 1 }), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default);
        await Until(() => Volatile.Read(ref f.Claims) >= 2);
        await worker.StopAsync(default);
        Assert.Equal(expectedSends, f.Requests.Count);
        Assert.Equal(expectedSends, f.Attempts.Count);
        Assert.True(expectedSends < 10); // No send or write for any remaining claimed row.
    }
    [Test]
    public async Task Idle_backoff_doubles_to_thirty_and_shutdown_prevents_another_claim()
    {
        var f = new WebhookFakes(); using var provider = Services(f);
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions()), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default);
        var delays = new[] { 2, 4, 8, 16, 30, 30 };
        for (var i = 0; i < delays.Length; i++)
        {
            await Until(() => f.Clock.Delays.Count > i);
            Assert.Equal(TimeSpan.FromSeconds(delays[i]), f.Clock.Delays[i]);
            if (i < delays.Length - 1) f.Clock.Advance(TimeSpan.FromSeconds(delays[i]));
        }
        var claims = f.Claims; await worker.StopAsync(default);
        f.Clock.Advance(TimeSpan.FromHours(1)); Assert.Equal(claims, f.Claims);
    }
    
    [TestCase(0, false)] [TestCase(1, true)] [TestCase(4, true)] [TestCase(16, true)] [TestCase(17, false)]
    public void Options_validate_limits(int count, bool valid)
    {
        var collection = new ServiceCollection().AddLogging();
        collection.AddDocAiWorker(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Webhooks:MaxParallelSends"] = count.ToString() }).Build());
        using var provider = collection.BuildServiceProvider();
        if (valid) Assert.Equal(count, provider.GetRequiredService<IOptions<WebhookOptions>>().Value.MaxParallelSends);
        else Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WebhookOptions>>().Value);
        Assert.True(new WebhookOptions().Enabled); Assert.Equal(4, new WebhookOptions().MaxParallelSends);
    }
    [Test]
    public async Task Disabled_sender_does_not_claim()
    {
        var f = new WebhookFakes(); using var provider = Services(f);
        using var worker = new WebhookSenderService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions { Enabled = false }), f.Clock, new TestLogger<WebhookSenderService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default); await worker.StopAsync(default); Assert.Equal(0, f.Claims);
    }
    private static ServiceProvider Services(WebhookFakes f)
    {
        var c = new ServiceCollection(); c.AddSingleton<IWebhookOutbox>(f); c.AddSingleton<ICallbackDirectory>(f);
        c.AddSingleton<ISecretStore>(f); c.AddSingleton<IWebhookTransport>(f); c.AddSingleton<TimeProvider>(f.Clock);
        c.AddSingleton<IWebhookPolicyService>(Rules.Get<IWebhookPolicyService>()); c.AddSingleton<IDocAiV2Json, DocAiV2Json>();
        c.AddSingleton(f.FailureLog); c.AddLogging(); c.AddScoped<DeliverWebhook>();
        return c.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
}
