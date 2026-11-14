using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_ActionCategory : Entity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
}
