using ISTDocAi.Application.Common.Contracts.ScheduledTask;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Entities.Public;

namespace ISTDocAi.Infrastructure.BackgroundServices;

public class MediatRTaskRunner : ITaskRunner
{
    public Task<TaskRunResult> RunAsync(Pub_ScheduledTask task, CancellationToken ct)
    {
        return Task.FromResult(TaskRunResult.Fail($"Unknown task name: {task.Name}"));
    }
}
