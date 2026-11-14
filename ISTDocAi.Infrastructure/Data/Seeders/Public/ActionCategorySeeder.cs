using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ISTDocAi.Infrastructure.Data.Seeders.Public;

public class ActionCategorySeeder : ISeeder
{
    public int Order => SeederOrder.ActionCategorySeeder;
    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        var domainAssembly = typeof(Entity).Assembly;

        var requiredActionCategories = domainAssembly
         .GetTypes()
         .Where(t => t.IsClass && t.GetCustomAttribute<ActionSeederAttribute>() != null)
         .Select(t => new Pub_ActionCategory
         {
             Id = t.Name,
             Name = t.Name
         })
         .ToList();

        // Retrieve all existing records in a single query
        var existingActionCategories = await context.Set<Pub_ActionCategory>()
            .ToDictionaryAsync(a => a.Id, cancellationToken: default);

        // Find items to update
        var actionCategoriesToUpdate = requiredActionCategories
            .Where(required => existingActionCategories.TryGetValue(required.Id, out var existing) &&
                   (existing.Name != required.Name))
            .Select(required => {
                var existing = existingActionCategories[required.Id];
                existing.Name = required.Name;
                return existing;
            })
            .ToList();

        // Find items to add
        var actionCategoriesToAdd = requiredActionCategories
            .Where(required => !existingActionCategories.ContainsKey(required.Id))
            .Select(required => new Pub_ActionCategory
            {
                Id = required.Id,
                Name = required.Name
            })
            .ToList();

        // Find items to delete
        var actionCategoriesToDelete = existingActionCategories
            .Where(existing => !requiredActionCategories.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        // Perform bulk delete
        if (actionCategoriesToDelete.Count != 0)
        {
            context.Set<Pub_ActionCategory>().RemoveRange(actionCategoriesToDelete);
        }

        // Perform bulk updates
        if (actionCategoriesToUpdate.Count != 0)
        {
            context.Set<Pub_ActionCategory>().UpdateRange(actionCategoriesToUpdate);
        }

        // Perform bulk inserts
        if (actionCategoriesToAdd.Count != 0)
        {
            await context.Set<Pub_ActionCategory>().AddRangeAsync(actionCategoriesToAdd);
        }

        // Save changes to the database
        await context.SaveChangesAsync();
    }
}
