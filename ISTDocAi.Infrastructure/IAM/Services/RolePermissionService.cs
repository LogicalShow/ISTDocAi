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

namespace ISTDocAi.Infrastructure.IAM.Services;

public class RolePermissionService(
    AppDbContext context,
    IUnitOfWork unitOfWork,
    RoleManager<IAM_Role> roleManager,
    AuthorizationHierarchyBuilder hierarchyBuilder,
    TimeProvider timeProvider,
    IUserService userService) : IRolePermissionService
{
    private const string PermissionClaimType = "permission";

    public async Task<Result<RolePermissionsDto>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null)
            return Result.Failure<RolePermissionsDto>(Error.NotFound(ErrorCodes.RoleNotFound, "Role not found"));

        var roleClaims = await roleManager.GetClaimsAsync(role);
        var assignedPermissions = roleClaims
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

        var result = new RolePermissionsDto
        {
            RoleId = role.Id,
            RoleNameEn = role.NameEn,
            RoleNameAr = role.NameAr,
            Applications = hierarchyBuilder.BuildPermissionTree(
                applications,
                allModules,
                assignedPermissions)
        };

        return Result.Success(result);
    }

    public async Task<Result> AssignRolePermissionsAsync(string roleId, IReadOnlyCollection<string> pageActionIds, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null)
            return Result.Failure(Error.NotFound(ErrorCodes.RoleNotFound, "Role not found"));

        var distinctPageActionIds = pageActionIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var permissionKeys = await context.IAM_PageActions
            .Where(pa => distinctPageActionIds.Contains(pa.Id) && pa.IsActive)
            .Select(pa => pa.PermissionKey)
            .ToListAsync(cancellationToken);

        if (permissionKeys.Count != distinctPageActionIds.Length)
            return Result.Failure(Error.Validation(ErrorCodes.PermissionInvalidPageActions, "One or more page action IDs are invalid or inactive"));

        var existingClaims = await context.RoleClaims
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType && rc.ClaimValue != null)
            .ToListAsync(cancellationToken);

        var changes = PermissionChangeCalculator.Calculate(
            existingClaims.Select(rc => rc.ClaimValue!),
            permissionKeys);

        if (changes.Count == 0)
            return Result.Success();

        context.RoleClaims.RemoveRange(existingClaims.Where(rc => changes.IsRevoked(rc.ClaimValue!)));

        context.RoleClaims.AddRange(changes.GrantedKeys().Select(key => new IdentityRoleClaim<string>
        {
            RoleId = roleId,
            ClaimType = PermissionClaimType,
            ClaimValue = key
        }));

        var audit = new IAM_PermissionAudit
        {
            EventType = AccessAuditEventType.RolePermissionsChanged,
            TargetRoleId = roleId,
            PermissionChanges = changes,
            PerformedByUserId = userService.UserId ?? "system",
            PerformedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        audit.SetEntityId();
        context.IAM_PermissionAudits.Add(audit);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
