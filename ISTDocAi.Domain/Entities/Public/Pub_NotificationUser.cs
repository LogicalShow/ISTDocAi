using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Domain.Entities.Public;

[ActionSeeder]
public class Pub_NotificationUser : AuditableEntity
{
    public string NotificationId { get; set; }

    public Pub_Notification Notification { get; set; }

    public string UserId { get; set; }

    public SendingWay SendingWay { get; set; }

    /// <summary>
    /// False means the user was un-assigned from this notification but keeps
    /// access to the Pub_NotificationUserDetail history already recorded against this row.
    /// </summary>
    public bool IsAssigned { get; set; } = true;

    public ICollection<Pub_NotificationUserDetail>? NotificationUserDetails { get; set; } // Navigation property

}
