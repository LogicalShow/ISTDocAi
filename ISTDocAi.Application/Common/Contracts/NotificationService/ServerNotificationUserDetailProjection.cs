using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Common.Contracts.NotificationService
{
    public record ServerNotificationUserDetailProjection : IProjectable<Pub_NotificationUserDetail, ServerNotificationUserDetailProjection>
    {
      public  string NotificationId { get; set; }
        public string NotificationName { get; set; }
        public string? GeneralMessageEn { get; set; }
        public string? GeneralMessageAr { get; set; }
        public string? TargetUrl { get; set; }
        public string? TargetId { get; set; }
        public string? CustomMessage { get; set; }
        public DateTime? SendingDate { get; set; }
        public DateTime? ActionDate { get; set; }
        public int? ReceivedNotificationStatus { get; set; }
        public string CorrelationId { get; set; }
        public static Expression<Func<Pub_NotificationUserDetail, ServerNotificationUserDetailProjection>> Selector => x=> new ServerNotificationUserDetailProjection
        {
        NotificationId= x.NotificationUser.NotificationId,
        NotificationName= x.NotificationUser.Notification.Name,
        GeneralMessageEn= x.NotificationUser.Notification.GeneralMessageEn,
        GeneralMessageAr= x.NotificationUser.Notification.GeneralMessageAr,
        TargetUrl= x.NotificationUser.Notification.TargetURL,
        TargetId= x.TargetId,
        CustomMessage= x.CustomMessage,
        SendingDate= x.SendingDate,
        ActionDate= x.ReadDate,
        ReceivedNotificationStatus= (int?)x.ReceivedNotificationStatus,
        CorrelationId= x.CorrelationId
        };
    }
}
