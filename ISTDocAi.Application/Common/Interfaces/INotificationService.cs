using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.NotificationService;

namespace ISTDocAi.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task PublishToOldNotificationsAsync(string userId, CancellationToken ct);
        Task PublishToOldNotificationsAsync(string userId, string connectionId, CancellationToken ct);
        Task PublishToNotificationAsync(string notificationId, ServerNotificationUserDetail payload, CancellationToken ct);
        Task NotifySubscriptionsChangedAsync(string userId, CancellationToken ct);

        Task AddUserToAssienedNotificationGroups(string userId, string connectionId, CancellationToken ct);
        Task AddUserToAssienedNotificationGroups(CancellationToken ct);
    }
}
