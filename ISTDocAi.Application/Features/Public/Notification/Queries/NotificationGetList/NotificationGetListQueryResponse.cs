using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetList
{
    public record NotificationGetListQueryResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? GeneralMessageEn { get; set; }
        public string? GeneralMessageAr { get; set; }
    }
}
