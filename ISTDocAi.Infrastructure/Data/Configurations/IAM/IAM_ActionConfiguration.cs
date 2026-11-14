using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_ActionConfiguration : IEntityTypeConfiguration<IAM_Action>
{
    public void Configure(EntityTypeBuilder<IAM_Action> builder)
    {
        builder.ToTable("IAM_Action");

        builder.HasKey(a => a.Id);



        builder.Property(a => a.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.NameAr)
            .IsRequired()
            .HasMaxLength(100);


        builder.HasMany(a => a.PageActions)
            .WithOne(pa => pa.Action)
            .HasForeignKey(pa => pa.ActionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
