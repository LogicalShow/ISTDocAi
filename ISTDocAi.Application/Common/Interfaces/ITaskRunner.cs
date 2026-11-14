using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.ScheduledTask;

namespace ISTDocAi.Application.Common.Interfaces
{
    public interface ITaskRunner
    {
        Task<TaskRunResult> RunAsync(Pub_ScheduledTask task, CancellationToken ct);
    }
}
