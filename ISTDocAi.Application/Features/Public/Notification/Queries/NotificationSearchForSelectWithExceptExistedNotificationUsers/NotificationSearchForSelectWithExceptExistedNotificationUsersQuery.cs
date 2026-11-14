using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationSearchForSelectWithExceptExistedNotificationUsers
{
   public record NotificationSearchForSelectWithExceptExistedNotificationUsersQuery(string? term,int pageSize):IQuery<IEnumerable<NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem>>;


    public record NotificationSearchForSelectWithExceptExistedNotificationUsersLookupItem(string Id,string Name,string GeneralMessageEn,string GeneralMessageAr);
}
