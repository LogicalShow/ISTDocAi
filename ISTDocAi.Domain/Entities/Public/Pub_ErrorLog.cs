using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_ErrorLog : Entity
{
    [Required]
    public DateTime ErrorTime { get; set; }

    [Required]
    [StringLength(100)]
    public string ErrorTypeName { get; set; }

    [Required]
    [StringLength(100)]
    public string ErrorMessage { get; set; }

    [StringLength(200)]
    public string? StackTrace { get; set; }

    [StringLength(200)]
    public string? Details { get; set; }

}
