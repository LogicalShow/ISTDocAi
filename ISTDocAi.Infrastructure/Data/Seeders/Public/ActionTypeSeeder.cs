using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ISTDocAi.Infrastructure.Data.Seeders.Public;

public class ActionTypeSeeder : ISeeder
{
    public int Order => SeederOrder.ActionTypeSeeder;
    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        var domainAssembly = typeof(Entity).Assembly;

        var requiredActionTypes = domainAssembly
    .GetTypes()
    .Where(t => t.IsClass && t.GetCustomAttribute<ActionSeederAttribute>() is not null)
    .SelectMany(entity =>
    {
        var attr = entity.GetCustomAttribute<ActionSeederAttribute>()!;
        var entityName = entity.Name;

        var defaultActions = new[] { "Add", "Update", "Delete" };
        var allActions = defaultActions
            .Concat(attr.CustomActions ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return allActions.Select(action =>
        {
            bool isDefault = defaultActions.Contains(action, StringComparer.OrdinalIgnoreCase);
            var actionCode = isDefault
                ? $"{entityName}{action}"   // Add, Update, Delete
                : $"{entityName}_{action}"; // Custom actions

            return new
            {
                Id = actionCode,
                Name = actionCode,
                ActionCategoryID = entityName
            };
        });
    })
    .ToArray();

        // Retrieve all existing records in a single query
        var existingActionTypes = await context.Set<Pub_ActionType>()
            .ToDictionaryAsync(a => a.Id, cancellationToken: default);

        // Find items to add
        var actionTypesToAdd = requiredActionTypes
            .Where(required => !existingActionTypes.ContainsKey(required.Id))
            .Select(required => new Pub_ActionType
            {
                Id = required.Id,
                Name = required.Name,
                ActionCategoryID = required.ActionCategoryID
            })
            .ToList();

        // Find items to update
        var actionTypesToUpdate = requiredActionTypes
            .Where(required => existingActionTypes.TryGetValue(required.Id, out var existing) &&
                   (existing.Name != required.Name || existing.ActionCategoryID != required.ActionCategoryID))
            .Select(required =>
            {
                var existing = existingActionTypes[required.Id];
                existing.Name = required.Name;
                existing.ActionCategoryID = required.ActionCategoryID;
                return existing;
            })
            .ToList();

        // Find items to delete
        var actionTypesToDelete = existingActionTypes
            .Where(existing => !requiredActionTypes.Any(required => required.Id == existing.Key))
            .Select(existing => existing.Value)
            .ToList();

        // Perform bulk delete
        if (actionTypesToDelete.Any())
        {
            context.Set<Pub_ActionType>().RemoveRange(actionTypesToDelete);
        }

        // Perform bulk updates
        if (actionTypesToUpdate.Any())
        {
            context.Set<Pub_ActionType>().UpdateRange(actionTypesToUpdate);
        }

        // Perform bulk inserts
        if (actionTypesToAdd.Any())
        {
            await context.Set<Pub_ActionType>().AddRangeAsync(actionTypesToAdd);
        }

        // Save changes to the database
        await context.SaveChangesAsync();
    }
}
