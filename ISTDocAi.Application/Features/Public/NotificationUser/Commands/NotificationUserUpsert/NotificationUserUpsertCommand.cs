using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert.Contracts;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert
{
   public record NotificationUserUpsertCommand(string ExistedNotificationId,string NewNotificationId,
     List<SelectedNotificationUser> SelectedNotificationUsers
       ): ICommand<NotificationUserUpsertCommandResponse>;

}
