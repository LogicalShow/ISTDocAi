using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails
{
    public record NotificationGetWithDetailsQuery(string NotificationId) : IQuery<NotificationGetWithDetailsQueryResponse>;
   
}
