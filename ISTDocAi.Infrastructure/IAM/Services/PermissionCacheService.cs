using System.Collections.Concurrent;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Server-side cache of each user's effective permissions (their direct "permission" claims)
/// and active status. Permissions are no longer carried in the JWT; authorization and the
/// client both read them from here.
///
/// Entries load lazily on the first authenticated request and are invalidated through
/// <see cref="Invalidate"/> when a <c>UserPermissionsChangedNotification</c> is published.
/// A per-user generation counter guarantees that a load which overlaps an invalidation is
/// never left in the cache.
///
/// Known limitation: the cache, the MediatR notification and the SignalR groups are all
/// process-local. A multi-node deployment needs a distributed cache/event bus and a SignalR backplane.
/// </summary>
public sealed class PermissionCacheService(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IPermissionCacheService
{
    private const string PermissionClaimType = "permission";

    private static readonly MemoryCacheEntryOptions EntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(20),
        // Safety net for changes made outside the application (e.g. direct database edits).
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    };

    private readonly ConcurrentDictionary<string, UserEntryState> _states = new(StringComparer.Ordinal);

    public async Task<PermissionSnapshot> GetAsync(string userId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey(userId), out PermissionSnapshot? cached))
        {
            return cached!;
        }

        var state = _states.GetOrAdd(userId, static _ => new UserEntryState());
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey(userId), out cached))
            {
                return cached!;
            }

            var generation = Volatile.Read(ref state.Generation);
            var snapshot = await LoadAsync(userId, cancellationToken);

            if (generation == Volatile.Read(ref state.Generation))
            {
                cache.Set(CacheKey(userId), snapshot, EntryOptions);

                // An invalidation may have landed between the check above and Set:
                // drop the entry so the stale snapshot is not served.
                if (generation != Volatile.Read(ref state.Generation))
                {
                    cache.Remove(CacheKey(userId));
                }
            }

            return snapshot;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public void Invalidate(string userId)
    {
        var state = _states.GetOrAdd(userId, static _ => new UserEntryState());
        Interlocked.Increment(ref state.Generation);
        cache.Remove(CacheKey(userId));
    }

    private async Task<PermissionSnapshot> LoadAsync(string userId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var version = timeProvider.GetUtcNow().UtcTicks;

        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new PermissionSnapshot(false, false, new Dictionary<string, int>(), new HashSet<string>(), version);
        }

        var permissionKeys = await context.UserClaims
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ClaimType == PermissionClaimType && c.ClaimValue != null)
            .Select(c => c.ClaimValue!)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        var compressionService = scope.ServiceProvider.GetRequiredService<IPermissionCompressionService>();
        var compressed = compressionService.CompressPermissions(permissionKeys);

        return new PermissionSnapshot(true, user.IsActive, compressed, permissionKeys, version);
    }

    private static string CacheKey(string userId) => $"permissions:{userId}";

    private sealed class UserEntryState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public long Generation;
    }
}
