using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_PageConfiguration : IEntityTypeConfiguration<IAM_Page>
{
    public void Configure(EntityTypeBuilder<IAM_Page> builder)
    {
        builder.ToTable("IAM_Page");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ModuleId)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(p => p.ParentPageId)
            .HasMaxLength(16);

        builder.Property(p => p.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.NameAr)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Route)
            .HasMaxLength(200);

        builder.Property(p => p.ShowInSidebar)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.OpenInNewTab)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(p => p.ParentPage)
            .WithMany(p => p.ChildPages)
            .HasForeignKey(p => p.ParentPageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PageActions)
            .WithOne(pa => pa.Page)
            .HasForeignKey(pa => pa.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.ParentPageId);

    }
}
