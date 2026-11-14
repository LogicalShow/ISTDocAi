namespace ISTDocAi.Application.Common.Interfaces.IAM;

/// <summary>
/// A user's effective permissions and status as held in the server-side permission cache.
/// </summary>
/// <param name="Compressed">Permissions in the compressed bitflag format sent to the client.</param>
/// <param name="Keys">Individual permission keys ({PageId}.{ActionId}).</param>
/// <param name="Version">Load timestamp; changes whenever the entry is reloaded.</param>
public sealed record PermissionSnapshot(
    bool UserExists,
    bool IsActive,
    IReadOnlyDictionary<string, int> Compressed,
    IReadOnlySet<string> Keys,
    long Version)
{
    public bool IsAllowed => UserExists && IsActive;
}

/// <summary>
/// Server-side cache of user permissions. Permissions are not carried in the JWT.
/// </summary>
public interface IPermissionCacheService
{
    Task<PermissionSnapshot> GetAsync(string userId, CancellationToken cancellationToken);

    void Invalidate(string userId);
}
