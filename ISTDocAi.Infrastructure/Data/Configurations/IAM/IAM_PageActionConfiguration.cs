using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_PageActionConfiguration : IEntityTypeConfiguration<IAM_PageAction>
{
    public void Configure(EntityTypeBuilder<IAM_PageAction> builder)
    {
        builder.ToTable("IAM_PageAction");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.PageId)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(pa => pa.ActionId)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(pa => pa.PermissionKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(pa => pa.PermissionKey).IsUnique();
        builder.HasIndex(pa => new { pa.PageId, pa.ActionId }).IsUnique();
        builder.HasIndex(pa => pa.PageId);
        builder.HasIndex(pa => pa.ActionId);
    }
}
