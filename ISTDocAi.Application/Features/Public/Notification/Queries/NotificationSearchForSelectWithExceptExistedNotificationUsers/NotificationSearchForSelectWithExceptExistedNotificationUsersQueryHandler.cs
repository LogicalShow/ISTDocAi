using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationSearchForSelectWithExceptExistedNotificationUsers
{
    public class NotificationSearchForSelectWithExceptExistedNotificationUsersQueryHandler(IRepositoryOp<Pub_Notification> repositoryOp) : IQueryHandler<NotificationSearchForSelectWithExceptExistedNotificationUsersQuery, IEnumerable<NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem>>
    {
        public async Task<Result<IEnumerable<NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem>>> Handle(NotificationSearchForSelectWithExceptExistedNotificationUsersQuery request, CancellationToken cancellationToken)
        {
            var result = await repositoryOp.GetListAsync(
                predicate: n => (string.IsNullOrEmpty(request.term) || n.GeneralMessageEn.Contains(request.term) || n.GeneralMessageAr.Contains(request.term))
                             && !n.NotificationUsers.Any(nu => nu.IsAssigned),
                orderBy: q => q.OrderBy(n => n.GeneralMessageEn),
                1,
                request.pageSize,
                cancellationToken: cancellationToken
            );

            var lookupItems = result.Items.Select(n => new NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem
            ( n.Id,
               n.Name,
              n.GeneralMessageEn,
               n.GeneralMessageAr
            )).ToList();

            return Result.Success<IEnumerable<NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem>>(lookupItems);
        }
    }
}
