using System;

namespace ISTDocAi.Domain.Entities;

public class ServiceJobInfo
{
    public string ServiceName { get; set; }
    public DateTime PlannedStartTime { get; set; }
    public int TimeIntervalMinutes { get; set; }
    public DateTime? LastRunStartTime { get; set; }
    public DateTime? LastRunFinishTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string JobId { get; set; }
} 