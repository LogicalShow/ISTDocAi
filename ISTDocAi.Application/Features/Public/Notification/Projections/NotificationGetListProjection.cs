using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Notification.Projections
{
    public record NotificationGetListProjection : IProjectable<Pub_Notification, NotificationGetListProjection>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? GeneralMessageEn { get; set; }
        public string? GeneralMessageAr { get; set; }
        public static Expression<Func<Pub_Notification, NotificationGetListProjection>> Selector => x=>new NotificationGetListProjection 
        { 
        Id = x.Id,
        Name = x.Name,
        GeneralMessageEn = x.GeneralMessageEn,
        GeneralMessageAr = x.GeneralMessageAr,
        };
    }
}
