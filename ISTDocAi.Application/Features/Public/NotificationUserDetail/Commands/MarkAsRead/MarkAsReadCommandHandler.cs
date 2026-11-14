using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Common.Services;

namespace ISTDocAi.Application.Features.Public.NotificationUserDetail.Commands.MarkAsRead;

public class MarkAsReadCommandHandler(
    IRepository<Pub_NotificationUserDetail> repository,
    IRepositoryOp<Pub_NotificationUserDetail> repositoryOp,
    IUserService userService,
    IActionLogService actionLogService,
    IUnitOfWork unitOfWork,
    INotificationService notificationService) :
    ICommandHandler<MarkAsReadCommand, MarkAsReadCommandResponse>
{
    public async Task<Result<MarkAsReadCommandResponse>> Handle(MarkAsReadCommand request, CancellationToken cancellationToken)
    {

        var entities = await repositoryOp.GetListAsyncIncludes(x => x.CorrelationId == request.CorrelationId && x.NotificationUser.NotificationId == request.NotificationId && x.NotificationUser.UserId == userService.UserId && x.ReceivedNotificationStatus == ReceivedNotificationStatus.Pending, [x => x.NotificationUser], cancellationToken: cancellationToken, ignoreFilters: true);
        if (entities == null || entities.Items.Count() == 0)
        {
            await notificationService.PublishToOldNotificationsAsync(userService.UserId, cancellationToken);
            return Result.Success(new MarkAsReadCommandResponse("Notification Readed"));
        }
        var entity = entities.Items.FirstOrDefault();

        entity.ReceivedNotificationStatus = Domain.Common.Enums.ReceivedNotificationStatus.Readed;

        await repository.UpdateAsync(entity, cancellationToken);

        await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_NotificationUserDetailUpdate, entity.Id, null, entity, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.PublishToOldNotificationsAsync(userService.UserId, cancellationToken);


        return Result.Success(new MarkAsReadCommandResponse("Notification Readed"));
    }
}
