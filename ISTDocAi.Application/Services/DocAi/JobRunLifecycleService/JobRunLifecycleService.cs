using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Application.Services.DocAi.StateMachinesService;

namespace ISTDocAi.Application.Services.DocAi.JobRunLifecycleService;
public sealed class JobRunLifecycleService(IStateMachinesService _stateMachinesService) : IJobRunLifecycleService
{
    public Decision<JobRunSnapshot> Finish(JobRunSnapshot entity, RunStatus status, DateTimeOffset now, DomainError? error = null) => !_stateMachinesService.CanTransition(entity.Status, status) || (status == RunStatus.Failed && error is null) ? Decision<JobRunSnapshot>.Reject(Rejection.InvalidTransition, "Only an Active run can finish; failure requires an error.") : Decision<JobRunSnapshot>.Accept(entity with { Status = status, CompletedAtUtc = now, Error = status == RunStatus.Failed ? error : null });
}
