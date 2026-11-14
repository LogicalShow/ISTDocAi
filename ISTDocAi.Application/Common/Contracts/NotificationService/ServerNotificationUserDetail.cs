using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Common.Contracts.NotificationService
{
    public record ServerNotificationUserDetail(
    string NotificationId,
    string NotificationName,
    string? GeneralMessageEn,
    string? GeneralMessageAr,
    string? TargetUrl,
    string? TargetId,
    string? CustomMessage,
    DateTime? SendingDate,
    DateTime? ActionDate,
    int? ReceivedNotificationStatus,
    string CorrelationId
);
}
