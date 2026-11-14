using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

public class Pub_ScheduledTaskLog : Entity
{
    public string ScheduledTaskId { get; set; }
    public Pub_ScheduledTask ScheduledTask { get; set; }
    public DateTime ExecutionTimeUtc { get; set; }
    public double? DurationSeconds { get; set; }
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

}
