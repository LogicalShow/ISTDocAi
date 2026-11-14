using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;

namespace ISTDocAi.Application.Services.DocAi.JobLifecycleService;
public interface IJobLifecycleService
{
    JobSnapshot Create(string? requestedShippingLineCode, bool hasActiveGenericModel, DateTimeOffset now);
    Decision<JobSnapshot> StartNewRun(JobSnapshot entity, bool reprocessRequested, bool hasActiveGenericModel, DateTimeOffset now, bool modelChanged = false);
    Decision<JobSnapshot> Claim(JobSnapshot entity, Guid newToken, DateTimeOffset now);
    Decision<JobSnapshot> Renew(JobSnapshot entity, Guid token, DateTimeOffset now);
    Decision<JobSnapshot> Release(JobSnapshot entity, Guid token, DateTimeOffset now, TimeSpan delay, string? errorCode = null);
    Decision<JobSnapshot> Retry(JobSnapshot entity, Guid token, DateTimeOffset now, TimeSpan? retryAfter = null, string? errorCode = null);
    Decision<JobSnapshot> Fail(JobSnapshot entity, Guid token, DomainError error, DateTimeOffset now, bool countsAsAttempt = false, long? operationId = null);
    Decision<JobSnapshot> EnforceTakeoverBudget(JobSnapshot entity, Guid token, DateTimeOffset now);
    Decision<JobSnapshot> BeginOperation(JobSnapshot entity, Guid token, long operationId, OperationKind kind, string modelId, DateTimeOffset now);
    Decision<JobSnapshot> RecordAccepted(JobSnapshot entity, long operationId, string operationLocation, DateTimeOffset now);
    Decision<JobSnapshot> CloseSending(JobSnapshot entity, Guid token, long operationId, AzureOperationState state, DateTimeOffset now);
    Decision<JobSnapshot> ClassifierSucceeded(JobSnapshot entity, Guid token, long operationId, string? mappedLine, double confidence, double minClassConfidence, DateTimeOffset now);
    Decision<JobSnapshot> ExtractionSucceeded(JobSnapshot entity, Guid token, long operationId, string rawResultPath, DateTimeOffset now);
    Decision<JobSnapshot> Complete(JobSnapshot entity, Guid token, DateTimeOffset now);
    ResumeAction Resume(JobSnapshot entity);
}
