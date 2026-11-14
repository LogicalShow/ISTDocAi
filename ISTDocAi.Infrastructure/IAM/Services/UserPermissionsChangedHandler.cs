using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Features.IAM.Events;
using ISTDocAi.Infrastructure.Realtime;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Observer of <see cref="UserPermissionsChangedNotification"/>: drops the affected users'
/// cached permissions, then tells their connected clients over SignalR so they reload
/// permissions and menu without logging out.
///
/// MediatR only scans the Application assembly, so this handler is registered explicitly
/// in Infrastructure's DependencyInjection.
/// </summary>
public sealed class UserPermissionsChangedHandler(
    IPermissionCacheService permissionCache,
    IHubContext<RealtimeHub> hubContext,
    ILogger<UserPermissionsChangedHandler> logger) : INotificationHandler<UserPermissionsChangedNotification>
{
    public const string ClientMethod = "PermissionsChanged";

    public async Task Handle(UserPermissionsChangedNotification notification, CancellationToken cancellationToken)
    {
        var userIds = notification.UserIds.Distinct(StringComparer.Ordinal).ToList();

        // Invalidate first, for every user, so the server stops authorizing with stale
        // permissions even if a push below fails.
        foreach (var userId in userIds)
        {
            permissionCache.Invalidate(userId);
        }

        foreach (var userId in userIds)
        {
            try
            {
                await hubContext.Clients
                    .Group(RealtimeHub.UserGroup(userId))
                    .SendAsync(ClientMethod, new { reason = notification.Reason.ToString() }, cancellationToken);
            }
            catch (Exception ex)
            {
                // The client also reloads on reconnect, and the cache is already invalidated.
                logger.LogWarning(ex, "Failed to push {Method} to user {UserId}", ClientMethod, userId);
            }
        }
    }
}
