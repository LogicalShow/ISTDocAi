using System;

namespace ISTDocAi.Domain.Events;

public class ServiceJobExecutedEvent
{
    public string ServiceName { get; }
    public DateTime ExecutionStartTime { get; }
    public DateTime ExecutionEndTime { get; }
    public bool WasSuccessful { get; }
    public string? ErrorMessage { get; }

    public ServiceJobExecutedEvent(string serviceName, DateTime executionStartTime, DateTime executionEndTime, bool wasSuccessful, string? errorMessage = null)
    {
        ServiceName = serviceName;
        ExecutionStartTime = executionStartTime;
        ExecutionEndTime = executionEndTime;
        WasSuccessful = wasSuccessful;
        ErrorMessage = errorMessage;
    }
} 