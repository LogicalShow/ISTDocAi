using ISTDocAi.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Data.Configurations.Common;

public static class DomainEntityModelBuilderExtensions
{
    public static ModelBuilder ConfigureDomainEntityConventions(this ModelBuilder modelBuilder)
    {
        var domainEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(Entity).IsAssignableFrom(entityType.ClrType))
            .ToList();

        foreach (var entityType in domainEntityTypes)
        {
            var entityBuilder = modelBuilder.Entity(entityType.ClrType);

            entityBuilder.HasKey(nameof(Entity.Id));
            entityBuilder.Property(nameof(Entity.Id)).HasMaxLength(16);

            if (typeof(ConcurrentEntity).IsAssignableFrom(entityType.ClrType))
            {
                entityBuilder.Property(nameof(ConcurrentEntity.RowVersion)).IsRowVersion();
            }

            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                entityBuilder.Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(450);
                entityBuilder.Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(450);
            }
        }

        return modelBuilder;
    }
}
