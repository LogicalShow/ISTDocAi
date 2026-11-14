using ISTDocAi.Application.Common.Contracts.NotificationService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.NotificationUserDetail.Commands.MarkAsRead
{
     public record MarkAsReadCommandResponse(string Message);
 //   public record MarkAsReadCommandResponse(List<ServerNotificationUserDetail> NotificationUserDetails);
}
