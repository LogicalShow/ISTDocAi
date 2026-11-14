using ISTDocAi.Domain.Entities.IAM;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Infrastructure.Data.Seeders.IAM;

public class AuthorizationSeeder : ISeeder
{
    public int Order => SeederOrder.AuthorizationSeeder;

    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        await SeedActionsAsync(context);
        await SeedApplicationsAsync(context);
        await SeedModulesAsync(context);
        await SeedPagesAsync(context);
        await SeedPageActionsAsync(context);
    }

    private async Task SeedActionsAsync(AppDbContext context)
    {
        var requiredActions = AuthorizationSeedData.Actions;
        var existingActions = await context.IAM_Actions.ToDictionaryAsync(a => a.Id);

        var actionsToAdd = requiredActions
            .Where(required => !existingActions.ContainsKey(required.Id))
            .Select(required => new IAM_Action { Id = required.Id, NameEn = required.NameEn, NameAr = required.NameAr, DisplayOrder = required.DisplayOrder })
            .ToList();

        var actionsToUpdate = requiredActions
            .Where(required => existingActions.TryGetValue(required.Id, out var existing) && (existing.NameEn != required.NameEn || existing.NameAr != required.NameAr || existing.DisplayOrder != required.DisplayOrder))
            .Select(required =>
            {
                var existing = existingActions[required.Id];
                existing.NameEn = required.NameEn;
                existing.NameAr = required.NameAr;
                existing.DisplayOrder = required.DisplayOrder;
                return existing;
            })
            .ToList();

        var actionsToDelete = existingActions
            .Where(existing => !requiredActions.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        if (actionsToDelete.Any()) context.IAM_Actions.RemoveRange(actionsToDelete);
        if (actionsToUpdate.Any()) context.IAM_Actions.UpdateRange(actionsToUpdate);
        if (actionsToAdd.Any()) await context.IAM_Actions.AddRangeAsync(actionsToAdd);

        await context.SaveChangesAsync();
    }

    private async Task SeedApplicationsAsync(AppDbContext context)
    {
        var requiredApplications = AuthorizationSeedData.Applications;
        var existingApplications = await context.IAM_Applications.ToDictionaryAsync(a => a.Id);

        var applicationsToAdd = requiredApplications
            .Where(required => !existingApplications.ContainsKey(required.Id))
            .Select(required => new IAM_Application { Id = required.Id, NameEn = required.NameEn, NameAr = required.NameAr, DisplayOrder = required.DisplayOrder, IsActive = required.IsActive })
            .ToList();

        var applicationsToUpdate = requiredApplications
            .Where(required => existingApplications.TryGetValue(required.Id, out var existing) && (existing.NameEn != required.NameEn || existing.NameAr != required.NameAr || existing.DisplayOrder != required.DisplayOrder || existing.IsActive != required.IsActive))
            .Select(required =>
            {
                var existing = existingApplications[required.Id];
                existing.NameEn = required.NameEn;
                existing.NameAr = required.NameAr;
                existing.DisplayOrder = required.DisplayOrder;
                existing.IsActive = required.IsActive;
                return existing;
            })
            .ToList();

        var applicationsToDelete = existingApplications
            .Where(existing => !requiredApplications.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        if (applicationsToDelete.Any()) context.IAM_Applications.RemoveRange(applicationsToDelete);
        if (applicationsToUpdate.Any()) context.IAM_Applications.UpdateRange(applicationsToUpdate);
        if (applicationsToAdd.Any()) await context.IAM_Applications.AddRangeAsync(applicationsToAdd);

        await context.SaveChangesAsync();
    }

    private async Task SeedModulesAsync(AppDbContext context)
    {
        var requiredModules = AuthorizationSeedData.Modules;
        var existingModules = await context.IAM_Modules.ToDictionaryAsync(m => m.Id);

        var modulesToAdd = requiredModules
            .Where(required => !existingModules.ContainsKey(required.Id))
            .Select(required => new IAM_Module { Id = required.Id, ApplicationId = required.ApplicationId, ParentId = required.ParentId, NameEn = required.NameEn, NameAr = required.NameAr, Icon = required.Icon, DisplayOrder = required.DisplayOrder, IsActive = required.IsActive })
            .ToList();

        var modulesToUpdate = requiredModules
            .Where(required => existingModules.TryGetValue(required.Id, out var existing) && (existing.ApplicationId != required.ApplicationId || existing.ParentId != required.ParentId || existing.NameEn != required.NameEn || existing.NameAr != required.NameAr || existing.Icon != required.Icon || existing.DisplayOrder != required.DisplayOrder || existing.IsActive != required.IsActive))
            .Select(required =>
            {
                var existing = existingModules[required.Id];
                existing.ApplicationId = required.ApplicationId;
                existing.ParentId = required.ParentId;
                existing.NameEn = required.NameEn;
                existing.NameAr = required.NameAr;
                existing.Icon = required.Icon;
                existing.DisplayOrder = required.DisplayOrder;
                existing.IsActive = required.IsActive;
                return existing;
            })
            .ToList();

        var modulesToDelete = existingModules
            .Where(existing => !requiredModules.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        if (modulesToDelete.Any()) context.IAM_Modules.RemoveRange(modulesToDelete);
        if (modulesToUpdate.Any()) context.IAM_Modules.UpdateRange(modulesToUpdate);
        if (modulesToAdd.Any()) await context.IAM_Modules.AddRangeAsync(modulesToAdd);

        await context.SaveChangesAsync();
    }


    private async Task SeedPagesAsync(AppDbContext context)
    {
        var requiredPages = AuthorizationSeedData.Pages;
        var existingPages = await context.IAM_Pages.ToDictionaryAsync(p => p.Id);

        var pagesToAdd = requiredPages
            .Where(required => !existingPages.ContainsKey(required.Id))
            .Select(required => new IAM_Page { Id = required.Id, ModuleId = required.ModuleId, ParentPageId = required.ParentPageId, NameEn = required.NameEn, NameAr = required.NameAr, Route = required.Route, ShowInSidebar = required.ShowInSidebar, OpenInNewTab = required.OpenInNewTab, DisplayOrder = required.DisplayOrder, IsActive = required.IsActive })
            .ToList();

        var pagesToUpdate = requiredPages
            .Where(required => existingPages.TryGetValue(required.Id, out var existing) && (existing.ModuleId != required.ModuleId || existing.ParentPageId != required.ParentPageId || existing.NameEn != required.NameEn || existing.NameAr != required.NameAr || existing.Route != required.Route || existing.ShowInSidebar != required.ShowInSidebar || existing.OpenInNewTab != required.OpenInNewTab || existing.DisplayOrder != required.DisplayOrder || existing.IsActive != required.IsActive))
            .Select(required =>
            {
                var existing = existingPages[required.Id];
                existing.ModuleId = required.ModuleId;
                existing.ParentPageId = required.ParentPageId;
                existing.NameEn = required.NameEn;
                existing.NameAr = required.NameAr;
                existing.Route = required.Route;
                existing.ShowInSidebar = required.ShowInSidebar;
                existing.OpenInNewTab = required.OpenInNewTab;
                existing.DisplayOrder = required.DisplayOrder;
                existing.IsActive = required.IsActive;
                return existing;
            })
            .ToList();

        var pagesToDelete = existingPages
            .Where(existing => !requiredPages.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        if (pagesToDelete.Any()) context.IAM_Pages.RemoveRange(pagesToDelete);
        if (pagesToUpdate.Any()) context.IAM_Pages.UpdateRange(pagesToUpdate);
        if (pagesToAdd.Any()) await context.IAM_Pages.AddRangeAsync(pagesToAdd);

        await context.SaveChangesAsync();
    }

    private async Task SeedPageActionsAsync(AppDbContext context)
    {
        var requiredPageActions = AuthorizationSeedData.Pages
            .SelectMany(page => page.Actions.Select(action => new { PageId = page.Id, ActionId = action.ToString(), PermissionKey = $"{page.Id}.{action}" }))
            .ToArray();

        var existingPageActions = await context.IAM_PageActions
            .Select(pa => new { pa.Id, pa.PageId, pa.ActionId, pa.PermissionKey, pa.IsActive })
            .ToDictionaryAsync(pa => $"{pa.PageId}_{pa.ActionId}");

        var pageActionsToAdd = requiredPageActions
            .Where(required => !existingPageActions.ContainsKey($"{required.PageId}_{required.ActionId}"))
            .Select(required => new IAM_PageAction { Id = Extensions.GenerateEntityId(), PageId = required.PageId, ActionId = required.ActionId, PermissionKey = required.PermissionKey, IsActive = true })
            .ToList();

        var pageActionsToUpdate = requiredPageActions
            .Where(required => existingPageActions.TryGetValue($"{required.PageId}_{required.ActionId}", out var existing) && (existing.PermissionKey != required.PermissionKey || !existing.IsActive))
            .Select(required =>
            {
                var key = $"{required.PageId}_{required.ActionId}";
                var existingId = existingPageActions[key].Id;
                return new { Id = existingId, required.PageId, required.ActionId, required.PermissionKey };
            })
            .ToList();

        // Create a set of required keys for efficient lookup
        var requiredKeys = requiredPageActions
            .Select(pa => $"{pa.PageId}_{pa.ActionId}")
            .ToHashSet();

        // Find page actions to delete by comparing with the required keys
        var pageActionsToDelete = existingPageActions
            .Where(existing => !requiredKeys.Contains(existing.Key))
            .Select(existing => new IAM_PageAction { Id = existing.Value.Id })
            .ToList();

        // Attach entities for deletion
        if (pageActionsToDelete.Any())
        {
            foreach (var item in pageActionsToDelete)
            {
                context.IAM_PageActions.Attach(item);
            }
        }

        if (pageActionsToDelete.Any()) context.IAM_PageActions.RemoveRange(pageActionsToDelete);

        if (pageActionsToUpdate.Any())
        {
            foreach (var update in pageActionsToUpdate)
            {
                var existing = await context.IAM_PageActions.FindAsync(update.Id);
                if (existing != null)
                {
                    existing.PermissionKey = update.PermissionKey;
                    existing.IsActive = true;
                }
            }
        }

        if (pageActionsToAdd.Any()) await context.IAM_PageActions.AddRangeAsync(pageActionsToAdd);

        await context.SaveChangesAsync();
    }
}
