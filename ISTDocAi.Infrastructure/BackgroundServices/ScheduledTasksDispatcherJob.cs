using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Common.Shared;
using ISTDocAi.Domain.Entities.Public;
using Quartz;

namespace ISTDocAi.Infrastructure.BackgroundServices;

[DisallowConcurrentExecution]
public class ScheduledTasksDispatcherJob(ITaskSchedulerService scheduler, ITaskRunner runner, TimeProvider timeProvider) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalDateTime();
        var due = await scheduler.GetDueTasksAsync(now, take: 50, context.CancellationToken);

        var lockedBy = Environment.MachineName;
        var lockedUntil = now.AddSeconds(15);

        foreach (var task in due)
        {
            var locked = await scheduler.TryLockTaskAsync(task.Id, lockedBy, lockedUntil, now, cancellationToken);
            if (!locked) continue;

            var startTimestamp = timeProvider.GetTimestamp();

            try
            {
                var result = await runner.RunAsync(task, context.CancellationToken);
                var duration = timeProvider.GetElapsedTime(startTimestamp).TotalSeconds;

                await scheduler.LogExecutionAsync(new Pub_ScheduledTaskLog
                {
                    Id = Extensions.GenerateEntityId(),
                    ScheduledTaskId = task.Id,
                    ExecutionTimeUtc = timeProvider.GetLocalDateTime(),
                    DurationSeconds = duration,
                    IsSuccess = result.IsSuccess,
                    ErrorMessage = result.IsSuccess ? null : result.Message
                }, cancellationToken);

                // one-time = deactivate
                var isOneTime = task.RunAtUtc.HasValue && !task.IntervalMinutes.HasValue;
                await scheduler.MarkExecutedAsync(task.Id, timeProvider.GetLocalDateTime(), deactivateIfOneTime: isOneTime, context.CancellationToken);
            }
            catch (Exception ex)
            {
                var duration = timeProvider.GetElapsedTime(startTimestamp).TotalSeconds;

                await scheduler.LogExecutionAsync(new Pub_ScheduledTaskLog
                {
                    Id = Extensions.GenerateEntityId(),
                    ScheduledTaskId = task.Id,
                    ExecutionTimeUtc = timeProvider.GetLocalDateTime(),
                    DurationSeconds = duration,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                }, cancellationToken);

                await scheduler.ReleaseLockAsync(task.Id, cancellationToken);
            }
        }
    }
}
