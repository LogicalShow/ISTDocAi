using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Common.SharedModels;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserListPaginated
{
    public class NotificationUserListPaginatedQueryHandler(
        IUserService userService,
        IRepositoryOp<Pub_NotificationUserDetail> repositoryOp
    ) : IQueryHandler<NotificationUserListPaginatedQuery, GetListResult<ServerNotificationUserDetail>>
    {
        public async Task<Result<GetListResult<ServerNotificationUserDetail>>> Handle(
            NotificationUserListPaginatedQuery request,
            CancellationToken cancellationToken)
        {
            System.Linq.Expressions.Expression<Func<Pub_NotificationUserDetail, bool>> predicate =
                request.UnreadOnly
                    ? x => x.NotificationUser.UserId == userService.UserId
                           && x.ReceivedNotificationStatus == ReceivedNotificationStatus.Pending
                    : x => x.NotificationUser.UserId == userService.UserId;

            var notifications = await repositoryOp.GetListAsyncIncludes(
                predicate,
                selector: ServerNotificationUserDetailProjection.Selector,
                new[] { "NotificationUser", "NotificationUser.Notification" },
                orderBy: q => q.OrderByDescending(x => x.SendingDate),
                pageNo: request.PageNo,
                pageRowCount: request.PageRowCount,
                cancellationToken: cancellationToken,
                ignoreFilters: true);

            var items = notifications.Items.Select(x => new ServerNotificationUserDetail(
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

            var result = new GetListResult<ServerNotificationUserDetail>
            {
                Items = items,
                TotalCount = notifications.TotalCount
            };

            return Result<GetListResult<ServerNotificationUserDetail>>.Success(result);
        }
    }
}
