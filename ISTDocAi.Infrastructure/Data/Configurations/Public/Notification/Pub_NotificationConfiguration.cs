using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.Notification;

public class Pub_NotificationConfiguration : IEntityTypeConfiguration<Pub_Notification>
{
    public void Configure(EntityTypeBuilder<Pub_Notification> builder)
    {
        builder.HasMany(x=>x.NotificationUsers)
               .WithOne(x=>x.Notification)
               .HasForeignKey(x=>x.NotificationId);
    }
}
