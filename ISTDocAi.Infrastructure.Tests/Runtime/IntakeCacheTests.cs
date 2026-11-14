using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Security.Cryptography;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;


using Microsoft.Extensions.DependencyInjection;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class IntakeCacheTests
{
    
    [TestCase(true)]
    [TestCase(false)]
    public async Task Hits_do_not_wait_for_the_other_caches_refresh(bool refreshingConsumers)
    {
        using var fixture = new Fixture();
        // Stagger loading so only the cache under refresh has expired.
        if (refreshingConsumers) await fixture.Cache.AuthenticateAsync("test", default);
        else await fixture.Cache.ReadAsync(default);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(30);
        if (refreshingConsumers) await fixture.Cache.ReadAsync(default);
        else await fixture.Cache.AuthenticateAsync("test", default);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(30);
        fixture.Source.Block = true;
        var refresh = refreshingConsumers ? (Task)fixture.Cache.AuthenticateAsync("test", default) : fixture.Cache.ReadAsync(default);
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var hit = refreshingConsumers ? (Task)fixture.Cache.ReadAsync(default) : fixture.Cache.AuthenticateAsync("test", default);
            // A cache hit completes synchronously, even while the other source is blocked.
            Assert.True(hit.IsCompletedSuccessfully);
            await hit;
        }
        finally { fixture.Source.Release.TrySetResult(); }
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public async Task Concurrent_misses_refresh_once(bool consumers)
    {
        using var fixture = new Fixture();
        fixture.Source.Block = true;
        var requests = Enumerable.Range(0, 20).Select(_ => consumers
            ? (Task)fixture.Cache.AuthenticateAsync("test", default) : fixture.Cache.ReadAsync(default)).ToArray();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, consumers ? fixture.Source.ConsumerReads : fixture.Source.CatalogReads);
        fixture.Source.Release.TrySetResult();
        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, consumers ? fixture.Source.ConsumerReads : fixture.Source.CatalogReads);
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public async Task Snapshot_expires_at_exactly_sixty_seconds(bool consumers)
    {
        using var fixture = new Fixture();
        Task Read() => consumers ? (Task)fixture.Cache.AuthenticateAsync("test", default) : fixture.Cache.ReadAsync(default);
        await Read();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60).AddTicks(-1);
        await Read();
        Assert.Equal(1, consumers ? fixture.Source.ConsumerReads : fixture.Source.CatalogReads);
        fixture.Clock.Now = fixture.Clock.Now.AddTicks(1);
        await Read();
        Assert.Equal(2, consumers ? fixture.Source.ConsumerReads : fixture.Source.CatalogReads);
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public async Task Failed_refresh_propagates_without_serving_expired_snapshot(bool consumers)
    {
        using var fixture = new Fixture();
        Task Read() => consumers ? (Task)fixture.Cache.AuthenticateAsync("test", default) : fixture.Cache.ReadAsync(default);
        await Read();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        fixture.Source.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(Read);
        await Assert.ThrowsAsync<InvalidOperationException>(Read);
        Assert.Equal(3, consumers ? fixture.Source.ConsumerReads : fixture.Source.CatalogReads);
        fixture.Source.Fail = false;
        fixture.Source.Active = false;
        if (consumers) Assert.Null(await fixture.Cache.AuthenticateAsync("test", default));
        else Assert.Empty(await fixture.Cache.ReadAsync(default));
    }
    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider provider;
        public Clock Clock { get; } = new();
        public Sources Source { get; } = new();
        public IntakeCache Cache { get; }
        public Fixture()
        {
            provider = new ServiceCollection().AddSingleton<IConsumerSource>(Source).AddSingleton<IModelCatalog>(Source).BuildServiceProvider();
            Cache = new(provider.GetRequiredService<IServiceScopeFactory>(), Clock);
        }
        public void Dispose() => provider.Dispose();
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Sources : IConsumerSource, IModelCatalog
    {
        public int ConsumerReads, CatalogReads;
        public bool Block, Fail;
        public bool Active = true;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private async Task Read(CancellationToken ct)
        {
            if (Block) { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); }
            if (Fail) throw new InvalidOperationException("Test refresh failure");
        }
        public async Task<IReadOnlyList<Consumer>> GetConsumersAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref ConsumerReads);
            await Read(ct);
            return Active ? [new(1, "TEST", "TEST", SHA256.HashData("test"u8), null, 10, 100, false)] : [];
        }
        public async Task<IReadOnlyList<CatalogModel>> GetCatalogAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref CatalogReads);
            await Read(ct);
            return Active ? [new("BL", null, "extract", "generic", .8m)] : [];
        }
        public Task ActivateAsync(string id, decimal accuracy, CancellationToken ct) => throw new NotSupportedException();
    }
}
