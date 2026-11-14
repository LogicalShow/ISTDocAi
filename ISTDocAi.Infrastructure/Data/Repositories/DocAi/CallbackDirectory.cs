using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class CallbackDirectory(IServiceScopeFactory scopes, TimeProvider clock) : ICallbackDirectory
{
    private readonly SemaphoreSlim gate = new(1);
    private IReadOnlyList<CallbackConsumer>? cached;
    private DateTimeOffset expires;
    public async Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (cached is not null && expires > clock.GetUtcNow()) return cached;
            await using var scope = scopes.CreateAsyncScope();
            var value = await scope.ServiceProvider.GetRequiredService<ICallbackSource>().GetCallbacksAsync(ct);
            cached = value; expires = clock.GetUtcNow().AddSeconds(60);
            return value;
        }
        finally { gate.Release(); }
    }
}
