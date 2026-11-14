using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Domain.Common.Constants.Public.Notification;
using ISTDocAi.Domain.Entities.Public;

namespace ISTDocAi.Infrastructure.Data.Seeders.Public;

public class NotificationSeeder : ISeeder
{
    public int Order => SeederOrder.NotificationSeeder;

    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        if (context.Pub_Notifications.Any())
        {
            return;
        }

        await context.Pub_Notifications.AddAsync(new Pub_Notification
        {
            Id = NotificationIdConstants.SystemNotification,
            Name = "System Notification",
            GeneralMessageEn = "System notification.",
            GeneralMessageAr = "",
            TargetURL = "",
            EventNameAndLocation = "System"
        });

        await context.SaveChangesAsync();
    }
}
