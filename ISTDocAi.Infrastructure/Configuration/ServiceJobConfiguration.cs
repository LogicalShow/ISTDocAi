using System;
using System.Collections.Generic;
using ISTDocAi.Domain.Entities;

namespace ISTDocAi.Infrastructure.Configuration;

public class ServiceJobConfiguration
{
    public List<ServiceJobInfo> Jobs { get; set; } = new();
}

public static class ServiceJobConfigurationExtensions
{
    public static ServiceJobConfiguration AddJob(
        this ServiceJobConfiguration config,
        string serviceName,
        DateTime plannedStartTime,
        int timeIntervalMinutes)
    {
        config.Jobs.Add(new ServiceJobInfo
        {
            ServiceName = serviceName,
            PlannedStartTime = plannedStartTime,
            TimeIntervalMinutes = timeIntervalMinutes,
            JobId = $"{serviceName}-{Guid.NewGuid()}"
        });
        return config;
    }
} 