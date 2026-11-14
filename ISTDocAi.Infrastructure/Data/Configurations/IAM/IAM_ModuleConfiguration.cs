using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_ModuleConfiguration : IEntityTypeConfiguration<IAM_Module>
{
    public void Configure(EntityTypeBuilder<IAM_Module> builder)
    {
        builder.ToTable("IAM_Module");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ApplicationId)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(m => m.ParentId)
            .HasMaxLength(16);

        builder.Property(m => m.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.NameAr)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Icon)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Pages)
            .WithOne(p => p.Module)
            .HasForeignKey(p => p.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
