using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
namespace ISTDocAi.Infrastructure.Services.DocAi.Intake;
public sealed class IntakeCache(IServiceScopeFactory scopes, TimeProvider clock) : IConsumerDirectory, ICatalogReader
{
    private sealed record Snapshot<T>(IReadOnlyList<T> Value, DateTimeOffset ExpiresAt);
    private readonly SemaphoreSlim consumerGate = new(1, 1);
    private readonly SemaphoreSlim catalogGate = new(1, 1);
    private Snapshot<Consumer>? consumers;
    private Snapshot<CatalogModel>? models;

    public async Task<Consumer?> AuthenticateAsync(string key, CancellationToken ct)
    {
        var snapshot = Volatile.Read(ref consumers);
        if (snapshot is null || clock.GetUtcNow() >= snapshot.ExpiresAt)
            snapshot = await RefreshConsumersAsync(ct);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        try
        {
            Consumer? match = null;
            foreach (var consumer in snapshot.Value)
            {
                var first = CryptographicOperations.FixedTimeEquals(hash, consumer.ApiKeyHash1);
                var second = CryptographicOperations.FixedTimeEquals(hash, consumer.ApiKeyHash2 ?? new byte[32]);
                if (first | second) match = consumer;
            }
            return match;
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }
    private async Task<Snapshot<Consumer>> RefreshConsumersAsync(CancellationToken ct)
    {
        await consumerGate.WaitAsync(ct);
        try
        {
            var snapshot = Volatile.Read(ref consumers);
            if (snapshot is not null && clock.GetUtcNow() < snapshot.ExpiresAt) return snapshot;
            using var scope = scopes.CreateScope();
            var values = await scope.ServiceProvider.GetRequiredService<IConsumerSource>().GetConsumersAsync(ct);
            // Own both the collection and key buffers so source mutations cannot change a published snapshot.
            snapshot = new(values.Select(c => c with { ApiKeyHash1 = c.ApiKeyHash1.ToArray(), ApiKeyHash2 = c.ApiKeyHash2?.ToArray() }).ToImmutableArray(), clock.GetUtcNow().AddSeconds(60));
            Volatile.Write(ref consumers, snapshot);
            return snapshot;
        }
        finally { consumerGate.Release(); }
    }
    public async Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct)
    {
        var snapshot = Volatile.Read(ref models);
        if (snapshot is not null && clock.GetUtcNow() < snapshot.ExpiresAt) return snapshot.Value;
        await catalogGate.WaitAsync(ct);
        try
        {
            snapshot = Volatile.Read(ref models);
            if (snapshot is not null && clock.GetUtcNow() < snapshot.ExpiresAt) return snapshot.Value;
            using var scope = scopes.CreateScope();
            var values = await scope.ServiceProvider.GetRequiredService<IModelCatalog>().GetCatalogAsync(ct);
            snapshot = new(values.ToImmutableArray(), clock.GetUtcNow().AddSeconds(60));
            Volatile.Write(ref models, snapshot);
            return snapshot.Value;
        }
        finally { catalogGate.Release(); }
    }
}
