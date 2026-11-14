using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_UserConfiguration : IEntityTypeConfiguration<IAM_User>
{
    public void Configure(EntityTypeBuilder<IAM_User> builder)
    {
        builder.ToTable("IAM_User");

        builder.Property(u => u.FullName)
            .HasMaxLength(256);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.IsAdministrator)
            .HasDefaultValue(false);

        builder.Property(u => u.RoleId)
            .IsRequired(false)
            .HasMaxLength(450);

        builder.Property(u => u.Email)
            .IsRequired();

        // Add unique constraint for Email
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_IAM_User_Email_Unique");

        // UserName is already unique from Identity, but enforce it explicitly
        builder.HasIndex(u => u.UserName)
            .IsUnique()
            .HasDatabaseName("IX_IAM_User_UserName_Unique");
    }
}
