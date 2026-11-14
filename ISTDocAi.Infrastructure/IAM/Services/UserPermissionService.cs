using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared;
using ISTDocAi.Domain.Entities.IAM;
using ISTDocAi.Application.Features.IAM.Events;
using MediatR;

namespace ISTDocAi.Infrastructure.IAM.Services;

public class UserPermissionService(
    AppDbContext context,
    IUnitOfWork unitOfWork,
    UserManager<IAM_User> userManager,
    AuthorizationHierarchyBuilder hierarchyBuilder,
    TimeProvider timeProvider,
    IUserService userService,
    IPublisher publisher) : IUserPermissionService
{
    private const string PermissionClaimType = "permission";

    public async Task<Result<UserPermissionsDto>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure<UserPermissionsDto>(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        var userClaims = await userManager.GetClaimsAsync(user);
        var assignedPermissions = userClaims
            .Where(c => c.Type == PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        var applications = await context.IAM_Applications
            .Where(a => a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Load all modules with their pages and page actions
        var allModules = await context.IAM_Modules
            .Where(m => m.IsActive)
            .Include(m => m.Pages.Where(p => p.IsActive))
                .ThenInclude(p => p.PageActions.Where(pa => pa.IsActive))
                    .ThenInclude(pa => pa.Action)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var result = new UserPermissionsDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? "",
            Applications = hierarchyBuilder.BuildPermissionTree(
                applications,
                allModules,
                assignedPermissions)
        };

        return Result.Success(result);
    }

    public async Task<Result> AssignUserPermissionsAsync(
        string userId,
        IReadOnlyCollection<string> pageActionIds,
        string? sourceRoleId,
        CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            return Result.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        var distinctPageActionIds = pageActionIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var permissionKeys = await context.IAM_PageActions
            .Where(pa => distinctPageActionIds.Contains(pa.Id) && pa.IsActive)
            .Select(pa => pa.PermissionKey)
            .ToListAsync(cancellationToken);

        if (permissionKeys.Count != distinctPageActionIds.Length)
            return Result.Failure(Error.Validation(ErrorCodes.PermissionInvalidPageActions, "One or more page action IDs are invalid or inactive"));

        var source = PermissionChangeSource.DirectAssignment;
        if (!string.IsNullOrWhiteSpace(sourceRoleId))
        {
            if (!await context.Roles.AnyAsync(r => r.Id == sourceRoleId, cancellationToken))
                return Result.Failure(Error.NotFound(ErrorCodes.RoleNotFound, "Role not found"));

            var rolePermissionKeys = await context.RoleClaims
                .Where(rc => rc.RoleId == sourceRoleId &&
                             rc.ClaimType == PermissionClaimType &&
                             rc.ClaimValue != null &&
                             context.IAM_PageActions.Any(pa =>
                                 pa.IsActive && pa.PermissionKey == rc.ClaimValue))
                .Select(rc => rc.ClaimValue!)
                .ToListAsync(cancellationToken);

            if (rolePermissionKeys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(permissionKeys))
            {
                source = PermissionChangeSource.RoleImport;
            }
            else
            {
                sourceRoleId = null;
            }
        }

        await ReplaceUserPermissionsAsync(userId, permissionKeys, source, sourceRoleId, cancellationToken);

        return Result.Success();
    }

    // Writes only the changed claim rows and the audit row in a single SaveChanges, so both commit or neither does.
    private async Task ReplaceUserPermissionsAsync(
        string userId,
        IEnumerable<string> newPermissionKeys,
        PermissionChangeSource source,
        string? sourceRoleId,
        CancellationToken cancellationToken)
    {
        var existingClaims = await context.UserClaims
            .Where(uc => uc.UserId == userId && uc.ClaimType == PermissionClaimType && uc.ClaimValue != null)
            .ToListAsync(cancellationToken);

        var changes = PermissionChangeCalculator.Calculate(
            existingClaims.Select(uc => uc.ClaimValue!),
            newPermissionKeys);

        if (changes.Count == 0)
            return;

        context.UserClaims.RemoveRange(existingClaims.Where(uc => changes.IsRevoked(uc.ClaimValue!)));

        context.UserClaims.AddRange(changes.GrantedKeys().Select(key => new IdentityUserClaim<string>
        {
            UserId = userId,
            ClaimType = PermissionClaimType,
            ClaimValue = key
        }));

        var audit = new IAM_PermissionAudit
        {
            EventType = AccessAuditEventType.UserPermissionsChanged,
            TargetUserId = userId,
            PermissionChanges = changes,
            Source = source,
            SourceRoleId = sourceRoleId,
            PerformedByUserId = userService.UserId ?? "system",
            PerformedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        audit.SetEntityId();
        context.IAM_PermissionAudits.Add(audit);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Not cancellable: claims and their audit are committed, so cached permissions must be dropped.
        var reason = source == PermissionChangeSource.RoleImport
            ? PermissionChangeReason.PermissionsImported
            : PermissionChangeReason.PermissionsAssigned;
        await publisher.Publish(new UserPermissionsChangedNotification([userId], reason), CancellationToken.None);
    }
}
