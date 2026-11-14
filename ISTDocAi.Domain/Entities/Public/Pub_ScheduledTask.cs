using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;


public class Pub_ScheduledTask : Entity
{
    public string Name { get; set; } = string.Empty;

    public string? ReferenceId { get; set; }

    // Interval-based tasks
    public int? IntervalMinutes { get; set; }

    // One-time tasks
    public DateTime? RunAtUtc { get; set; }

    // Extra data for handler (ex: {"WorkOrderId":"...","RfrRequestId":"..."})
    public string? PayloadJson { get; set; }

    // Execution
    public DateTime? LastExecutionTimeUtc { get; set; }

    public bool IsActive { get; set; } = true;

    // Locking for multi-instance safety
    public DateTime? LockedUntilUtc { get; set; }

  
    public string? LockedBy { get; set; }

    // Navigation
    public ICollection<Pub_ScheduledTaskLog> ScheduledTaskLogs { get; set; } = new List<Pub_ScheduledTaskLog>();

}
