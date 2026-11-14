using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class ModelThresholdCacheTests
{
    [Test]
    public async Task Thresholds_are_cached_per_producing_model_for_sixty_seconds_including_retired_or_missing_models()
    {
        var source = new Source(); var clock = new TestClock();
        var services = new ServiceCollection(); services.AddScoped<IModelThresholdSource>(_ => source);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var cache = new ModelThresholdCache(provider.GetRequiredService<IServiceScopeFactory>(), clock);
        Assert.Equal(.8m, await cache.ReadThresholdAsync("retired", default));
        source.Value = .95m;
        Assert.Equal(.8m, await cache.ReadThresholdAsync("retired", default));
        Assert.Equal(.95m, await cache.ReadThresholdAsync("active", default));
        clock.Advance(TimeSpan.FromSeconds(59)); Assert.Equal(.8m, await cache.ReadThresholdAsync("retired", default));
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.Equal(.95m, await cache.ReadThresholdAsync("retired", default));
        Assert.Equal(3, source.Reads);
        source.Value = null; Assert.Null(await cache.ReadThresholdAsync("missing", default));
        Assert.Null(await cache.ReadThresholdAsync("missing", default)); Assert.Equal(4, source.Reads);
    }
    [Test]
    public async Task Concurrent_misses_are_coalesced_and_failed_reads_are_not_cached()
    {
        var source = new Source { Block = true }; var clock = new TestClock(); var services = new ServiceCollection();
        services.AddScoped<IModelThresholdSource>(_ => source); using var provider = services.BuildServiceProvider();
        var cache = new ModelThresholdCache(provider.GetRequiredService<IServiceScopeFactory>(), clock);
        var reads = Enumerable.Range(0, 10).Select(_ => cache.ReadThresholdAsync("retired", default)).ToArray();
        Assert.Equal(1, source.Reads); source.Release.SetResult(); await Task.WhenAll(reads); Assert.Equal(1, source.Reads);
        clock.Advance(TimeSpan.FromSeconds(60)); source.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => cache.ReadThresholdAsync("retired", default));
        source.Fail = false; Assert.Equal(.8m, await cache.ReadThresholdAsync("retired", default)); Assert.Equal(3, source.Reads);
    }
    private sealed class Source : IModelThresholdSource
    {
        public decimal? Value = .8m;
        public int Reads;
        public bool Fail;
        public bool Block;
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<decimal?> GetThresholdAsync(string modelId, CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            if (Block) await Release.Task.WaitAsync(ct);
            if (Fail) throw new IOException("Unavailable");
            return Value;
        }
    }
}
