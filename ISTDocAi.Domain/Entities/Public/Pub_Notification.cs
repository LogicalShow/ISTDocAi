using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

[ActionSeeder]
public class Pub_Notification : Entity
{
    public string Name { get; set; }

    public string? GeneralMessageEn { get; set; }

    public string? GeneralMessageAr { get; set; }

    public string? EventNameAndLocation { get; set; }

    public string? TargetURL { get; set; }

    public ICollection<Pub_NotificationUser> NotificationUsers { get; set; }

}
