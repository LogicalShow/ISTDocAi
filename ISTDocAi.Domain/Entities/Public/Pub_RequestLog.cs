using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_RequestLog : Entity
{
    [Required]
    [StringLength(100)]
    public string RequestTypeName { get; set; }

    [Required, MaxLength]
    public string RequestParameters { get; set; }
    
    [Required]
    public DateTime RequestInitTime { get; set; }

    [Required]
    public DateTime RequestCompleteTime { get; set; }

    [StringLength(1000)]
    public string? ResponseError { get; set; }

}
