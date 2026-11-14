using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.NotificationUserDetail.Commands.MarkAsRead
{
    public record MarkAsReadCommand(string NotificationId, string CorrelationId):ICommand<MarkAsReadCommandResponse>; 
 
   
}
