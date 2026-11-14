using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserList
{
    public class NotificationUserListQueryHandler(IUserService userService,IRepositoryOp<Pub_NotificationUserDetail> repositoryOp) : IQueryHandler<NotificationUserListQuery, List<ServerNotificationUserDetail>>
    {
        public async Task<Result<List<ServerNotificationUserDetail>>> Handle(NotificationUserListQuery request, CancellationToken cancellationToken)
        {
            var notifications = await repositoryOp.GetListAsyncIncludes(x => x.NotificationUser.UserId == userService.UserId,
    selector: ServerNotificationUserDetailProjection.Selector,
    new[] { "NotificationUser", "NotificationUser.Notification" }, orderBy: x => x.OrderByDescending(x => x.SendingDate), 1, 1000, cancellationToken: cancellationToken, true);


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

            return Result<List<ServerNotificationUserDetail>>.Success(oldReceivedNotifications);
        }
    }
}
