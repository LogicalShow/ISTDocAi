using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Common.Interfaces
{
    public interface ITaskSchedulerService
    {
        Task<List<Pub_ScheduledTask>> GetDueTasksAsync(DateTime utcNow, int take, CancellationToken ct);

        Task<bool> TryLockTaskAsync(string taskId, string lockedBy, DateTime lockedUntilUtc, DateTime utcNow, CancellationToken ct);

        Task MarkExecutedAsync(string taskId, DateTime executionTimeUtc, bool deactivateIfOneTime, CancellationToken ct);

        Task ReleaseLockAsync(string taskId, CancellationToken ct);

        Task LogExecutionAsync(Pub_ScheduledTaskLog log, CancellationToken ct);

        // Optional: create/upsert
        Task UpsertOneTimeTaskAsync(string name, string referenceId, DateTime runAtUtc, string payloadJson, CancellationToken ct);
    }
}
