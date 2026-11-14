using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Infrastructure.Services;

public class NotificationLogService(
    IRepository<Pub_Notification> repository,
    IRepository<Pub_NotificationUserDetail> notificationUserDetailRepository,
    TimeProvider timeProvider
    ) : INotificationLogService
{
    public async Task<Result<ServerNotificationUserDetail>> LogNotificationAsync(string notificationId, string? targetId, string? customMessage, CancellationToken ct)
    {
        var notification = await repository.GetAsync(
            notificationId,
            [entity => entity.NotificationUsers],
            cancellationToken: ct);
        if (notification == null)
        {
            return Result<ServerNotificationUserDetail>.Failure(Error.NotFound(ErrorCodes.NotificationNotFound, $"Notification with ID {notificationId} not found."));
        }

        // Only currently assigned users receive new deliveries. Rows with IsAssigned == false
        // keep their existing history but must not accumulate any more of it.
        var notificationUsers = notification.NotificationUsers?
            .Where(x => x.IsAssigned)
            .ToList() ?? [];

        if (notificationUsers.Count == 0)
        {
            return Result<ServerNotificationUserDetail>.Failure(Error.BusinessRule(ErrorCodes.NotificationNoUserAssigned, "No user assigned."));
        }

        var correlationId = Extensions.GenerateEntityId();
        var now = timeProvider.GetLocalDateTime();
        var payload = new ServerNotificationUserDetail
        (notification.Id,
            notification.Name,
            notification.GeneralMessageEn,
            notification.GeneralMessageAr,
            notification.TargetURL,
            targetId,
            customMessage,
            now,
            null,
            1,
            correlationId
        );
        // Save notification details to database
        var entities = new List<Pub_NotificationUserDetail>();
        foreach (var notificationUser in notificationUsers)
        {
            var entity = new Pub_NotificationUserDetail
            {
                Id = Extensions.GenerateEntityId(),
                NotificationUserId = notificationUser.Id,
                SendingDate = now,
                ReceivedNotificationStatus = ReceivedNotificationStatus.Pending,
                TargetId = targetId,
                CustomMessage = customMessage,
                CorrelationId = correlationId

            };
            await notificationUserDetailRepository.AddAsync(entity, ct);

        }
        return Result.Success(payload);
    }
}
