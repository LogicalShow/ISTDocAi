using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ISTDocAi.Infrastructure.Realtime
{
    public interface IRealtimeService
    {
        Task NotifyAsync(ServerNotification serverNotification, string? connIds, CancellationToken cancellationToken);
        Task NotifyAsync(List<ServerNotification> serverNotifications, string? connIds, CancellationToken cancellationToken);
        Task NotifyExceptAsync(ServerNotification serverNotification, string? excludedIds, CancellationToken cancellationToken);
        Task NotifyExceptAsync(List<ServerNotification> serverNotifications, string? excludedIds, CancellationToken cancellationToken);

       
    }

    public interface IBackgroundRealtimeService : IRealtimeService
    {
    }

    public class RealtimeService : IRealtimeService, IBackgroundRealtimeService
    {
        const string ServerNotificationsKey = "ServerNotifications";
        private readonly IHubContext<RealtimeHub> _hubContext;

        public RealtimeService(IHubContext<RealtimeHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyAsync(ServerNotification serverNotification, string? connIds, CancellationToken cancellationToken) =>
            await this.NotifyAsync(new List<ServerNotification> { serverNotification }, connIds, cancellationToken);

        public async Task NotifyAsync(List<ServerNotification> serverNotifications, string? connIds, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(connIds))
            {
                string[] connIdsList = connIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                await _hubContext.Clients.Clients(connIdsList).SendAsync(ServerNotificationsKey, serverNotifications);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync(ServerNotificationsKey, serverNotifications);
            }
        }

        public async Task NotifyExceptAsync(ServerNotification serverNotification, string? excludedIds, CancellationToken cancellationToken) =>
            await this.NotifyExceptAsync(new List<ServerNotification> { serverNotification }, excludedIds, cancellationToken);

        public async Task NotifyExceptAsync(List<ServerNotification> serverNotifications, string? excludedIds, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(excludedIds))
            {
                string[] excludedIdsList = excludedIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                await _hubContext.Clients.AllExcept(excludedIdsList).SendAsync(ServerNotificationsKey, serverNotifications);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync(ServerNotificationsKey, serverNotifications);
            }
        }


    }
}
