using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_ActionType : Entity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [StringLength(100)]
    [ForeignKey("Pub_ActionCategory")]
    public string ActionCategoryID { get; set; }

    public virtual Pub_ActionCategory Pub_ActionCategory { get; set; }    
}
