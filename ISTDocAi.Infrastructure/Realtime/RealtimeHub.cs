using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Infrastructure.Data;

namespace ISTDocAi.Infrastructure.Realtime;

[Authorize]
public class RealtimeHub : Hub
{
   private readonly AppDbContext _db;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RealtimeHub(AppDbContext db,IUserService userService, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
    }

  //public  override async Task OnConnectedAsync()
  //  {
        //var userId = _userService.UserId;
        //var context = _httpContextAccessor;
        //if (string.IsNullOrWhiteSpace(userId))
        //{
        //    await base.OnConnectedAsync();
        //    return;
        //}

      //  await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

    //var subscribedNotificationIds = await _db.Set<Pub_NotificationUser>()
    //    .AsNoTracking()
    //      .Where(x => x.UserId == userId)
    //     .Select(x => x.NotificationId)
    //      .Distinct()
    //      .ToListAsync();


   //   foreach (var notificationId in subscribedNotificationIds)
   //         await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroup(notificationId));

    //var oldData = await _db.Set<Pub_NotificationUserDetail>()
    //    .AsNoTracking().IgnoreQueryFilters()
    //    .Include(x => x.NotificationUser).ThenInclude(x => x.Notification)
    //    .Where(x => x.NotificationUser.UserId == userId)
    //    .OrderByDescending(x => x.SendingDate)
    //    .ToListAsync();

    //var old = oldData.Select(x => new ServerNotificationUserDetail
    //    (x.NotificationUser.NotificationId,
    //    x.NotificationUser.Notification.Name,
    //    x.NotificationUser.Notification.GeneralMessageEn,
    //    x.NotificationUser.Notification.GeneralMessageAr,
    //    x.NotificationUser.Notification.TargetURL,
    //    x.TargetId,
    //    x.CustomMessage,
    //    x.SendingDate,
    //     x.ActionDate,
    //    (int)x.ReceivedNotificationStatus,
    //    x.CorrelationId
    //    ))
    //    .ToList();

  //  await Clients.Caller.SendAsync("ReceiveOldNotifications", old);


    //    await base.OnConnectedAsync();
    //}

    public override async Task OnConnectedAsync()
    {
        var userId = _userService.UserId;
        
        if (string.IsNullOrWhiteSpace(userId))
        {
            Console.WriteLine($"[RealtimeHub] Connection rejected: User not authenticated. ConnectionId: {Context.ConnectionId}");
            Context.Abort();
            return;
        }

        Console.WriteLine($"[RealtimeHub] User {userId} connected with connection ID: {Context.ConnectionId}");

        // Add user to their personal group
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        // Get all notifications user is subscribed to
        var subscribedNotificationIds = await _db.Set<Pub_NotificationUser>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsAssigned && x.SendingWay != SendingWay.Email)
            .Select(x => x.NotificationId)
            .Distinct()
            .ToListAsync();

        // Add user to all their notification groups
        foreach (var notificationId in subscribedNotificationIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroup(notificationId));
        }

        // Send old notifications to the newly connected client
        var oldNotifications = await _db.Set<Pub_NotificationUserDetail>()
            .AsNoTracking()
            .Include(x => x.NotificationUser)
                .ThenInclude(x => x.Notification)
            .Where(x => x.NotificationUser.UserId == userId)
            .OrderByDescending(x => x.SendingDate)
            .Take(10)
            .Select(x => new ServerNotificationUserDetail(
                x.NotificationUser.NotificationId,
                x.NotificationUser.Notification.Name,
                x.NotificationUser.Notification.GeneralMessageEn,
                x.NotificationUser.Notification.GeneralMessageAr,
                x.NotificationUser.Notification.TargetURL,
                x.TargetId,
                x.CustomMessage,
                x.SendingDate,
                x.ReadDate,
                (int)x.ReceivedNotificationStatus,
                x.CorrelationId
            ))
            .ToListAsync();

        await Clients.Caller.SendAsync("ReceiveOldNotifications", oldNotifications);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public static string UserGroup(string userId) => $"usr:{userId}";
    private static string NotificationGroup(string notificationId) => $"ntf:{notificationId}";
}
