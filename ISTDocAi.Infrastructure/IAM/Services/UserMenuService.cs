using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;
using ISTDocAi.Domain.Entities.IAM;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Infrastructure.IAM.Services;

public class UserMenuService(
    UserManager<IAM_User> userManager,
    AppDbContext context,
    AuthorizationHierarchyBuilder hierarchyBuilder,
    IPermissionCacheService permissionCacheService) : IUserMenuService
{
    public async Task<Result<UserMenuDto>> GetUserMenuAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure<UserMenuDto>(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));
        }

        var effectivePermissions = (await permissionCacheService.GetAsync(userId, cancellationToken)).Keys;
        var modules = await LoadActiveModulesAsync(cancellationToken);
        var menu = hierarchyBuilder.BuildUserMenu(modules, effectivePermissions);

        return Result.Success(menu);
    }

    private Task<List<IAM_Module>> LoadActiveModulesAsync(CancellationToken cancellationToken)
    {
        return context.IAM_Modules
            .Where(module => module.IsActive)
            .Include(module => module.Pages.Where(page => page.IsActive))
                .ThenInclude(page => page.PageActions.Where(pageAction => pageAction.IsActive))
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }
}
