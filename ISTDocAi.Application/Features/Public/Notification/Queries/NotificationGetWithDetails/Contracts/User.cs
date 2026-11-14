using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails.Contracts
{
    public class User
    {
        public string Id { get; set; }

        public byte[] RowVersion { get; init; }

        public string UserId { get; set; }
        public string UserName { get; set; }

        public int SendingWayId { get; set; }   // Enum  value  System=1, Email=2,Both= 3

        public string SendingWayName { get; set; }


    }
}
