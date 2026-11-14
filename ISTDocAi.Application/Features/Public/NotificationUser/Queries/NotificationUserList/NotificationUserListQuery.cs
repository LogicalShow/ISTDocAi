using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserList
{
    public record NotificationUserListQuery():IQuery<List<ServerNotificationUserDetail>>;

}
