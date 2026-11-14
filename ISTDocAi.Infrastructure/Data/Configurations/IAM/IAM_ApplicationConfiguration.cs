using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_ApplicationConfiguration : IEntityTypeConfiguration<IAM_Application>
{
    public void Configure(EntityTypeBuilder<IAM_Application> builder)
    {
        builder.ToTable("IAM_Application");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.NameAr)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(a => a.Modules)
            .WithOne(m => m.Application)
            .HasForeignKey(m => m.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
