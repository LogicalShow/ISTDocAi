using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Infrastructure.IAM.Services;

internal static class PermissionChangeCalculator
{
    public static Dictionary<string, PermissionChangeType> Calculate(
        IEnumerable<string> previousPermissionKeys,
        IEnumerable<string> newPermissionKeys)
    {
        var previousKeys = previousPermissionKeys.ToHashSet();
        var newKeys = newPermissionKeys.ToHashSet();

        var changes = new Dictionary<string, PermissionChangeType>();

        foreach (var revokedKey in previousKeys.Except(newKeys))
            changes[revokedKey] = PermissionChangeType.Revoked;

        foreach (var grantedKey in newKeys.Except(previousKeys))
            changes[grantedKey] = PermissionChangeType.Granted;

        return changes;
    }

    public static bool IsRevoked(this Dictionary<string, PermissionChangeType> changes, string permissionKey) =>
        changes.TryGetValue(permissionKey, out var changeType) && changeType == PermissionChangeType.Revoked;

    public static IEnumerable<string> GrantedKeys(this Dictionary<string, PermissionChangeType> changes) =>
        changes.Where(change => change.Value == PermissionChangeType.Granted).Select(change => change.Key);
}
