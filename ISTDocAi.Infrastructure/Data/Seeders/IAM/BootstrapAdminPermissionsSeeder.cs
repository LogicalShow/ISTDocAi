using System.Security.Claims;
using ISTDocAi.Domain.Common.Constants.IAM;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Data.Seeders.IAM;

/// <summary>
/// Grants the seeded administrator enough access to manage IAM on a fresh database.
/// Once the administrator has any direct permission, permission management is left
/// entirely to the application.
/// </summary>
public sealed class BootstrapAdminPermissionsSeeder(
    ILogger<BootstrapAdminPermissionsSeeder> logger) : ISeeder
{
    private const string PermissionClaimType = "permission";

    public int Order => SeederOrder.BootstrapAdminPermissionsSeeder;

    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IAM_User>>();
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        var admin = await userManager.FindByIdAsync(SeededUserIds.Admin);
        if (admin is null)
        {
            logger.LogWarning(
                "Bootstrap administrator {AdminUserId} was not found. Permission bootstrap was skipped.",
                SeededUserIds.Admin);
            return;
        }

        var existingClaims = await userManager.GetClaimsAsync(admin);
        if (existingClaims.Any(claim => claim.Type == PermissionClaimType))
        {
            logger.LogDebug(
                "Bootstrap administrator {AdminUserId} already has permissions. Permission bootstrap was skipped.",
                admin.Id);
            return;
        }

        var permissionKeys = await context.IAM_PageActions
            .Where(pageAction =>
                pageAction.IsActive &&
                pageAction.Page.IsActive &&
                pageAction.Page.Module.IsActive &&
                pageAction.Page.Module.Application.IsActive)
            .Select(pageAction => pageAction.PermissionKey)
            .Distinct()
            .OrderBy(permissionKey => permissionKey)
            .ToListAsync();

        if (permissionKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "No active permissions were found for the bootstrap administrator.");
        }

        var claims = permissionKeys
            .Select(permissionKey => new Claim(PermissionClaimType, permissionKey));

        var result = await userManager.AddClaimsAsync(admin, claims);
        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException(
                $"Failed to bootstrap permissions for administrator '{admin.Id}': {errors}");
        }

        logger.LogInformation(
            "Granted {PermissionCount} IAM bootstrap permission(s) to administrator {AdminUserId}.",
            permissionKeys.Count,
            admin.Id);
    }
}
