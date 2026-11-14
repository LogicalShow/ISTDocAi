using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.Notification;

public class Pub_NotificationUserDetailConfiguration : IEntityTypeConfiguration<Domain.Entities.Public.Pub_NotificationUserDetail>
{
    public void Configure(EntityTypeBuilder<Pub_NotificationUserDetail> builder)
    {
        builder.Property(x => x.ReceivedNotificationStatus).HasDefaultValue(ReceivedNotificationStatus.Pending);
        builder.HasOne(x => x.NotificationUser).WithMany(x => x.NotificationUserDetails).HasForeignKey(x => x.NotificationUserId);
    }
}
