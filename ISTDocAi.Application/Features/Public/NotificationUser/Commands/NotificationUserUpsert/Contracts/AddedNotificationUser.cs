using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert.Contracts
{
    public class AddedNotificationUser
    {
        public string UserId { get; set; }

        public int SendingWayId { get; set; }   // Enum  value  System=1, Email=2,Both= 3

    }
}
