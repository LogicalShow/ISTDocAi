using ISTDocAi.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ISTDocAi.Infrastructure.Services;

public class IdempotencyService(IDistributedCache cache) : IIdempotencyService
{
    public Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        cache.GetStringAsync(key, cancellationToken);

    public async Task SetAsync(
        string key,
        string value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
        }

        await cache.SetStringAsync(
            key,
            value,
            options,
            cancellationToken);
    }
}
