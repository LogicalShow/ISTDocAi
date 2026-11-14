using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.EmailTemplates;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using ISTDocAi.Infrastructure.Realtime;

namespace ISTDocAi.Infrastructure.Services;

public class NotificationService(
    IHubContext<RealtimeHub> hubContext, 
    IRepositoryOp<Pub_NotificationUserDetail> notificationUserDetailRepositoryOp, 
    IUserService userService, 
    IRepositoryOp<Pub_NotificationUser> notificationUserrepositoryOp,
    IUserQueryService userQueryService,
    IEmailService emailService,
    IHttpContextAccessor httpContextAccessor) : INotificationService
{
    public async Task PublishToNotificationAsync(string notificationId, ServerNotificationUserDetail payload, CancellationToken ct)
    {
        // Send notification to all users subscribed to this notification via the notification group
        await hubContext.Clients.Group(NotificationGroup(notificationId))
            .SendAsync("ReceiveNotification", payload, ct);

        // Also send to each subscribed user's personal group as fallback
        // (covers cases where user is not in the ntf group due to stale membership or SendingWay filter)
        var subscribedUsers = await notificationUserrepositoryOp.GetListAsync(
            x => x.NotificationId == notificationId && x.IsAssigned && x.SendingWay != SendingWay.Email,
            null, cancellationToken: ct);

        foreach (var nu in subscribedUsers)
        {
            await hubContext.Clients.Group(UserGroup(nu.UserId))
                .SendAsync("ReceiveNotification", payload, ct);
        }

        //send notification to user Emails subscribed to this notification
        var emails = await userQueryService.GetUserEmailsByNotificationId(notificationId, ct);
        var htmlBody = NotificationEmailTemplates.BuildNotificationEmail(payload);

        var result = await emailService.SendEmailAsync(emails, "New Notification", htmlBody, cancellationToken: ct);
       
    }

    public Task NotifySubscriptionsChangedAsync(string userId, CancellationToken ct)
        => hubContext.Clients.All
            .SendAsync("SubscriptionsChanged", ct);

    public async Task PublishToOldNotificationsAsync(string userId, CancellationToken ct)
    {
        var notifications = await notificationUserDetailRepositoryOp.GetListAsyncIncludes(x => x.NotificationUser.UserId == userId,
            selector: ServerNotificationUserDetailProjection.Selector,
            new[] { "NotificationUser", "NotificationUser.Notification" }, orderBy: x => x.OrderByDescending(x => x.SendingDate), 1, 10, cancellationToken: ct, true);


        var oldReceivedNotifications = notifications.Items.Select(x => new ServerNotificationUserDetail(
            x.NotificationId,
            x.NotificationName,
            x.GeneralMessageEn,
            x.GeneralMessageAr,
            x.TargetUrl,
            x.TargetId,
            x.CustomMessage,
            x.SendingDate,
            x.ActionDate,
            x.ReceivedNotificationStatus,
            x.CorrelationId)).ToList();

        _ = hubContext.Clients.Group($"usr:{userId}").SendAsync("ReceiveOldNotifications", oldReceivedNotifications);


    }

    private static string UserGroup(string userId) => $"usr:{userId}";
    private static string NotificationGroup(string notificationId) => $"ntf:{notificationId}";

    public async Task PublishToOldNotificationsAsync(string userId, string connectionId, CancellationToken ct)
    {
        var notifications = await notificationUserDetailRepositoryOp.GetListAsyncIncludes(x => x.NotificationUser.UserId == userId && x.NotificationUser.SendingWay != SendingWay.Email,
            selector: ServerNotificationUserDetailProjection.Selector,
           new[] { "NotificationUser", "NotificationUser.Notification" }, orderBy: x => x.OrderByDescending(x => x.SendingDate), 1, 10, cancellationToken: ct, true);


              var oldReceivedNotifications = notifications.Items.Select(x => new ServerNotificationUserDetail(
                x.NotificationId,
                x.NotificationName,
                x.GeneralMessageEn,
                x.GeneralMessageAr,
                x.TargetUrl,
                x.TargetId,
                x.CustomMessage,
                x.SendingDate,
                x.ActionDate,
                x.ReceivedNotificationStatus,
                x.CorrelationId)).ToList();

            _ = hubContext.Clients.Client(connectionId).SendAsync("ReceiveOldNotifications", oldReceivedNotifications);
    }

    public async Task AddUserToAssienedNotificationGroups(string userId, string connectionId, CancellationToken ct)
    {
 
     if (string.IsNullOrEmpty(connectionId))
        {
            return;
        }

            var userNotifications = await notificationUserrepositoryOp.GetListAsync(x => x.UserId == userId && x.IsAssigned && x.SendingWay != SendingWay.Email,
              null,
              cancellationToken: ct);


              if (userNotifications.Count() == 0)
              {
                  return;
              }

              var subscribedNotificationIds = userNotifications.Select(x => x.NotificationId).Distinct();

              foreach (var notificationId in subscribedNotificationIds)
               {
                   await hubContext.Groups.AddToGroupAsync(connectionId, NotificationGroup(notificationId), ct);
               }           
             
        await hubContext.Groups.AddToGroupAsync(connectionId, UserGroup(userId), ct);

       
    }

    public async Task AddUserToAssienedNotificationGroups(CancellationToken ct)
    {
        var contexct = httpContextAccessor.HttpContext;
        var connectionId = contexct.Request.Headers["X-SignalR-ConnectionId"];
        if (!string.IsNullOrEmpty(connectionId))
        {
            var userNotifications = await notificationUserrepositoryOp.GetListAsync(x => x.UserId == userService.UserId && x.IsAssigned && x.SendingWay != SendingWay.Email,
            null,
            cancellationToken: ct);


            if (userNotifications.Count() == 0)
            {
                return;
            }

            var subscribedNotificationIds = userNotifications.Select(x => x.NotificationId).Distinct();

            foreach (var notificationId in subscribedNotificationIds)
            {
                await hubContext.Groups.AddToGroupAsync(connectionId, NotificationGroup(notificationId), ct);
            }

            await hubContext.Groups.AddToGroupAsync(connectionId, UserGroup(userService.UserId), ct);
        }
       
    }
}
