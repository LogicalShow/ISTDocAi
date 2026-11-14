using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;

namespace ISTDocAi.Application.Services.DocAi.JobRunLifecycleService;
public interface IJobRunLifecycleService
{
    Decision<JobRunSnapshot> Finish(JobRunSnapshot entity, RunStatus status, DateTimeOffset now, DomainError? error = null);
}
