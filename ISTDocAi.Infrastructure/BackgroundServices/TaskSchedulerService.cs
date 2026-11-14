using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Infrastructure.BackgroundServices;

public class TaskSchedulerService(AppDbContext db) : ITaskSchedulerService
{
    public async Task<List<Pub_ScheduledTask>> GetDueTasksAsync(DateTime utcNow, int take, CancellationToken ct)
    {
        return await db.Pub_ScheduledTasks
            .AsNoTracking()
            .Where(t => t.IsActive
                && (!t.LockedUntilUtc.HasValue || t.LockedUntilUtc.Value <= utcNow)
                && (
                    // Interval tasks
                    (t.IntervalMinutes.HasValue
                        && (!t.LastExecutionTimeUtc.HasValue
                            || EF.Functions.DateDiffMinute(t.LastExecutionTimeUtc.Value, utcNow) >= t.IntervalMinutes.Value))
                    ||
                    // One-time tasks
                    (t.RunAtUtc.HasValue
                        && t.RunAtUtc.Value <= utcNow
                        && !t.LastExecutionTimeUtc.HasValue)
                ))
            .OrderBy(t => t.RunAtUtc ?? DateTime.MaxValue)
            .ThenBy(t => t.LastExecutionTimeUtc ?? DateTime.MinValue)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> TryLockTaskAsync(string taskId, string lockedBy, DateTime lockedUntilUtc, DateTime utcNow, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Pub_ScheduledTasks
SET LockedBy = {lockedBy},
    LockedUntilUtc = {lockedUntilUtc}

WHERE Id = {taskId}
  AND IsActive = 1
  AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= {utcNow});
", ct);

        return affected == 1;
    }

    public async Task MarkExecutedAsync(string taskId, DateTime executionTimeUtc, bool deactivateIfOneTime, CancellationToken ct)
    {
        if (deactivateIfOneTime)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Pub_ScheduledTasks
SET LastExecutionTimeUtc = {executionTimeUtc},
    IsActive = 0,
    LockedBy = NULL,
    LockedUntilUtc = NULL

WHERE Id = {taskId};
", ct);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Pub_ScheduledTasks
SET LastExecutionTimeUtc = {executionTimeUtc},
    LockedBy = NULL,
    LockedUntilUtc = NULL
WHERE Id = {taskId};
", ct);
        }
    }

    public async Task ReleaseLockAsync(string taskId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Pub_ScheduledTasks
SET LockedBy = NULL,
    LockedUntilUtc = NULL
WHERE Id = {taskId};
", ct);
    }

    public async Task LogExecutionAsync(Pub_ScheduledTaskLog log, CancellationToken ct)
    {
        await db.Pub_ScheduledTaskLogs.AddAsync(log, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpsertOneTimeTaskAsync(string name, string referenceId, DateTime runAtUtc, string payloadJson, CancellationToken ct)
    {

        var existing = await db.Pub_ScheduledTasks
            .FirstOrDefaultAsync(t => t.IsActive && t.Name == name && t.ReferenceId == referenceId, ct);

        if (existing is null)
        {
            var task = new Pub_ScheduledTask
            {

                Name = name,
                ReferenceId = referenceId,
                RunAtUtc = runAtUtc,
                PayloadJson = payloadJson,
                LastExecutionTimeUtc = null,
                IsActive = true
            };
            task.SetEntityId();
            db.Pub_ScheduledTasks.Add(task);
        }
        else
        {
            existing.RunAtUtc = runAtUtc;
            existing.PayloadJson = payloadJson;
            existing.LastExecutionTimeUtc = null;
            existing.LockedBy = null;
            existing.LockedUntilUtc = null;
        }

        await db.SaveChangesAsync(ct);
    }
}
