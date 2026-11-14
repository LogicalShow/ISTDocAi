using Microsoft.EntityFrameworkCore;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.Notification;

public class Pub_NotificationUserConfiguration: IEntityTypeConfiguration<Domain.Entities.Public.Pub_NotificationUser>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.Entities.Public.Pub_NotificationUser> builder)
    {
        builder.HasOne(x => x.Notification)
               .WithMany(x => x.NotificationUsers)
               .HasForeignKey(x => x.NotificationId);

        builder.HasOne<IAM_User>()
                 .WithMany()
                 .HasForeignKey(x => x.UserId);

        builder.HasMany(x=>x.NotificationUserDetails)
               .WithOne(x=>x.NotificationUser)
               .HasForeignKey(x=>x.NotificationUserId);

        builder.Property(x => x.IsAssigned)
               .HasDefaultValue(true);

        builder.HasIndex(x => new { x.NotificationId, x.UserId })
               .IsUnique();

    }

}
