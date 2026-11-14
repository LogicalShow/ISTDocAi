using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_ActionLog : Entity
{
    [Required, ForeignKey("IAM_User")]
    public string UserID { get; set; }

    [Required, StringLength(100), ForeignKey("Pub_ActionType"),]
    public string ActionTypeID { get; set; }

    [Required, StringLength(16)]
    public string ActionID { get; set; }

    [StringLength(20)]
    public string? ActionNo { get; set; }

    [Required]
    public DateTime ActionTime { get; set; }

    [Required,MaxLength]
    public string ActionDetails { get; set; }

    public virtual Pub_ActionType Pub_ActionType { get; set; }

}
