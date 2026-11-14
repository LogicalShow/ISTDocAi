using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ISTDocAi.Infrastructure.Data.Interceptors;

public sealed class AuditableEntityInterceptor(
    TimeProvider timeProvider,
    IUserService userService) : SaveChangesInterceptor
{
    private const string SystemActor = "system";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var actor = userService.UserId ?? SystemActor;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.CreatedBy = actor;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Entity.UpdatedBy = actor;
                    entry.Property(entity => entity.CreatedAt).IsModified = false;
                    entry.Property(entity => entity.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}
