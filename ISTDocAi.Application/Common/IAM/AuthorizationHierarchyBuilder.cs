using ISTDocAi.Domain.Dtos.IAM;
using ISTDocAi.Domain.Entities.IAM;

namespace ISTDocAi.Application.Common.IAM;

public sealed class AuthorizationHierarchyBuilder
{
    private const string ViewActionId = "VIEW";

    public List<ApplicationModuleDto> BuildPermissionTree(
        IReadOnlyCollection<IAM_Application> applications,
        IReadOnlyCollection<IAM_Module> modules,
        IReadOnlySet<string> assignedPermissions)
    {
        var activeModules = modules
            .Where(module => module.IsActive)
            .ToList();
        var modulesByParentId = activeModules.ToLookup(module => module.ParentId);
        var rootModulesByApplicationId = activeModules
            .Where(module => module.ParentId == null)
            .ToLookup(module => module.ApplicationId);

        return applications
            .Where(application => application.IsActive)
            .OrderBy(application => application.DisplayOrder)
            .Select(application => new ApplicationModuleDto
            {
                Id = application.Id,
                NameEn = application.NameEn,
                NameAr = application.NameAr,
                Modules = rootModulesByApplicationId[application.Id]
                    .OrderBy(module => module.DisplayOrder)
                    .Select(module => BuildPermissionModule(
                        module,
                        modulesByParentId,
                        assignedPermissions))
                    .ToList()
            })
            .ToList();
    }

    public UserMenuDto BuildUserMenu(
        IReadOnlyCollection<IAM_Module> modules,
        IReadOnlySet<string> effectivePermissions)
    {
        var activeModules = modules
            .Where(module => module.IsActive)
            .ToList();
        var modulesByParentId = activeModules.ToLookup(module => module.ParentId);

        var rootModules = modulesByParentId[null]
            .OrderBy(module => module.DisplayOrder)
            .Select(module => BuildMenuModule(
                module,
                modulesByParentId,
                effectivePermissions))
            .Where(HasVisibleMenuContent)
            .ToList();

        return new UserMenuDto { Modules = rootModules };
    }

    private static ModuleDto BuildPermissionModule(
        IAM_Module module,
        ILookup<string?, IAM_Module> modulesByParentId,
        IReadOnlySet<string> assignedPermissions)
    {
        var activePages = module.Pages
            .Where(page => page.IsActive)
            .ToList();
        var childPagesByParentId = activePages
            .Where(page => page.ParentPageId != null)
            .ToLookup(page => page.ParentPageId);
        var rootPages = activePages
            .Where(page => page.ParentPageId == null)
            .OrderBy(page => page.DisplayOrder)
            .Select(page => BuildPermissionPage(
                page,
                childPagesByParentId,
                assignedPermissions))
            .ToList();

        var childModules = modulesByParentId[module.Id]
            .OrderBy(child => child.DisplayOrder)
            .ToList();
        var directPageEntity = ResolveDirectPage(module, childModules.Count > 0);
        var directPage = directPageEntity == null
            ? null
            : rootPages.FirstOrDefault(page => page.Id == directPageEntity.Id);

        return new ModuleDto
        {
            Id = module.Id,
            NameEn = module.NameEn,
            NameAr = module.NameAr,
            DirectPage = directPage,
            Pages = directPage == null ? rootPages : [],
            Children = childModules
                .Select(child => BuildPermissionModule(
                    child,
                    modulesByParentId,
                    assignedPermissions))
                .ToList()
        };
    }

    private static PageDto BuildPermissionPage(
        IAM_Page page,
        ILookup<string?, IAM_Page> childPagesByParentId,
        IReadOnlySet<string> assignedPermissions)
    {
        return new PageDto
        {
            Id = page.Id,
            NameEn = page.NameEn,
            NameAr = page.NameAr,
            Actions = page.PageActions
                .Where(pageAction => pageAction.IsActive)
                .OrderBy(pageAction => pageAction.Action.DisplayOrder)
                .Select(pageAction => new PageActionDto
                {
                    Id = pageAction.Id,
                    ActionId = pageAction.ActionId,
                    ActionNameEn = pageAction.Action.NameEn,
                    ActionNameAr = pageAction.Action.NameAr,
                    PermissionKey = pageAction.PermissionKey,
                    IsAssigned = assignedPermissions.Contains(pageAction.PermissionKey)
                })
                .ToList(),
            ChildPages = childPagesByParentId[page.Id]
                .OrderBy(child => child.DisplayOrder)
                .Select(child => BuildPermissionPage(
                    child,
                    childPagesByParentId,
                    assignedPermissions))
                .ToList()
        };
    }

    private static MenuModuleDto BuildMenuModule(
        IAM_Module module,
        ILookup<string?, IAM_Module> modulesByParentId,
        IReadOnlySet<string> effectivePermissions)
    {
        var childModules = modulesByParentId[module.Id]
            .OrderBy(child => child.DisplayOrder)
            .ToList();
        var directPage = ResolveDirectPage(module, childModules.Count > 0);
        var menuModule = new MenuModuleDto
        {
            Id = module.Id,
            NameEn = module.NameEn,
            NameAr = module.NameAr,
            Icon = module.Icon,
            DisplayOrder = module.DisplayOrder
        };

        if (directPage != null)
        {
            if (IsVisibleMenuPage(directPage, effectivePermissions))
            {
                menuModule.Route = directPage.Route;
                menuModule.OpenInNewTab = directPage.OpenInNewTab;
            }

            return menuModule;
        }

        menuModule.Pages = module.Pages
            .Where(page => page.IsActive && page.ParentPageId == null)
            .Where(page => IsVisibleMenuPage(page, effectivePermissions))
            .OrderBy(page => page.DisplayOrder)
            .Select(page => new MenuPageDto
            {
                Id = page.Id,
                NameEn = page.NameEn,
                NameAr = page.NameAr,
                Route = page.Route!,
                OpenInNewTab = page.OpenInNewTab,
                DisplayOrder = page.DisplayOrder
            })
            .ToList();

        menuModule.SubModules = childModules
            .Select(child => BuildMenuModule(
                child,
                modulesByParentId,
                effectivePermissions))
            .Where(HasVisibleMenuContent)
            .ToList();

        return menuModule;
    }

    private static IAM_Page? ResolveDirectPage(
        IAM_Module module,
        bool hasActiveChildModules)
    {
        if (hasActiveChildModules)
        {
            return null;
        }

        var activeRootPages = module.Pages
            .Where(page => page.IsActive && page.ParentPageId == null)
            .Take(2)
            .ToList();

        return activeRootPages.Count == 1
            ? activeRootPages[0]
            : null;
    }

    private static bool IsVisibleMenuPage(
        IAM_Page page,
        IReadOnlySet<string> effectivePermissions)
    {
        return page.ShowInSidebar &&
            !string.IsNullOrWhiteSpace(page.Route) &&
            page.PageActions.Any(pageAction =>
                pageAction.IsActive &&
                pageAction.ActionId == ViewActionId &&
                effectivePermissions.Contains(pageAction.PermissionKey));
    }

    private static bool HasVisibleMenuContent(MenuModuleDto module)
    {
        return !string.IsNullOrWhiteSpace(module.Route) ||
            module.Pages.Count > 0 ||
            module.SubModules.Count > 0;
    }

}
