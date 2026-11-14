using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class ModelThresholdCache(IServiceScopeFactory scopes, TimeProvider clock) : IModelThresholdReader
{
    private sealed record Entry(decimal? Value, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<decimal?> ReadThresholdAsync(string modelId, CancellationToken ct)
    {
        if (entries.TryGetValue(modelId, out var entry) && clock.GetUtcNow() < entry.Expires) return entry.Value;
        await gate.WaitAsync(ct);
        try
        {
            if (entries.TryGetValue(modelId, out entry) && clock.GetUtcNow() < entry.Expires) return entry.Value;
            using var scope = scopes.CreateScope();
            var value = await scope.ServiceProvider.GetRequiredService<IModelThresholdSource>().GetThresholdAsync(modelId, ct);
            entries[modelId] = new(value, clock.GetUtcNow().AddSeconds(60));
            return value;
        }
        finally { gate.Release(); }
    }
}
