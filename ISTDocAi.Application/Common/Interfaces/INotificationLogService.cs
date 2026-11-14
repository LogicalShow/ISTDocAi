using ISTDocAi.Domain.Common.Shared.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.NotificationService;

namespace ISTDocAi.Application.Common.Interfaces
{
    public interface INotificationLogService
    {
        Task<Result<ServerNotificationUserDetail>> LogNotificationAsync(string notificationId, string? targetId, string? customMessage, CancellationToken ct);
    }
}
