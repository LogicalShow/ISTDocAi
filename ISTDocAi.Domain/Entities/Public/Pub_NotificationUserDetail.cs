using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Domain.Entities.Public;

[ActionSeeder]
public class Pub_NotificationUserDetail : AuditableEntity
{
    public string NotificationUserId { get; set; }

    public Pub_NotificationUser NotificationUser { get; set; }

    public DateTime? SendingDate { get; set; }
    public ReceivedNotificationStatus ReceivedNotificationStatus { get; set; } = ReceivedNotificationStatus.Pending;

    public DateTime? ReadDate { get; set; }

    public string? TargetId { get; set; }

    public string? CustomMessage { get; set; }

    public string CorrelationId { get; set; }
}
