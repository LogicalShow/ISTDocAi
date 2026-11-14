using ISTDocAi.Application.Common.Interfaces.IAM;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Claims transformation that adds the user's "permission" claims from the server-side
/// permission cache, for use with authorization filters (RequirePermissionAttribute).
/// Permissions are not trusted from the token: any "p" (legacy compressed permissions)
/// or "permission" claim it carries is removed first.
/// </summary>
public sealed class PermissionClaimsTransformation(IPermissionCacheService permissionCacheService) : IClaimsTransformation
{
    private const string PermissionClaimType = "permission";
    private const string LegacyCompressedPermissionsClaimType = "p";

    // Marks the identity added by this transformation. It cannot come from a token,
    // so it also makes the transformation idempotent when it runs more than once per request.
    private const string AuthenticationType = "PermissionCache";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || principal.Identities.Any(identity => identity.AuthenticationType == AuthenticationType))
        {
            return principal;
        }

        foreach (var identity in principal.Identities)
        {
            var tokenPermissionClaims = identity.Claims
                .Where(c => c.Type is PermissionClaimType or LegacyCompressedPermissionsClaimType)
                .ToList();

            foreach (var claim in tokenPermissionClaims)
            {
                identity.TryRemoveClaim(claim);
            }
        }

        var permissionIdentity = new ClaimsIdentity(AuthenticationType);
        var userId = principal.FindFirst("uid")?.Value ?? principal.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var snapshot = await permissionCacheService.GetAsync(userId, CancellationToken.None);
            if (snapshot.IsAllowed)
            {
                permissionIdentity.AddClaims(snapshot.Keys.Select(key => new Claim(PermissionClaimType, key)));
            }
        }

        principal.AddIdentity(permissionIdentity);
        return principal;
    }
}
