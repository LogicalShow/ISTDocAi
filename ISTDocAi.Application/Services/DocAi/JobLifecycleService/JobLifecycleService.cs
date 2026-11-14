using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Application.Services.DocAi.AzureOperationLifecycleService;
using ISTDocAi.Application.Services.DocAi.JobRunLifecycleService;
using ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
using ISTDocAi.Application.Services.DocAi.LeasePolicyService;
using ISTDocAi.Application.Services.DocAi.ResumePolicyService;
using ISTDocAi.Application.Services.DocAi.RetryPolicyService;
using ISTDocAi.Application.Services.DocAi.StateMachinesService;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;

namespace ISTDocAi.Application.Services.DocAi.JobLifecycleService;
public sealed class JobLifecycleService(IClassifierPolicyService _classifierPolicyService, ILeasePolicyService _leasePolicyService, IResumePolicyService _resumePolicyService, IRetryPolicyService _retryPolicyService, IStateMachinesService _stateMachinesService, ITakeoverPolicyService _takeoverPolicyService, IAzureOperationLifecycleService _azureOperationLifecycleService, IJobRunLifecycleService _jobRunLifecycleService) : IJobLifecycleService
{
    public JobSnapshot Create(string? requestedShippingLineCode, bool hasActiveGenericModel, DateTimeOffset now) => new()
    {
        Status = JobStatus.Queued,
        Stage = _stateMachinesService.StartingStage(requestedShippingLineCode, hasActiveGenericModel),
        RequestedShippingLineCode = requestedShippingLineCode,
        ShippingLineCode = requestedShippingLineCode,
        CurrentRun = new(1, RunReason.New, RunStatus.Active, now),
        NextAttemptAtUtc = now
    };
    public Decision<JobSnapshot> StartNewRun(JobSnapshot entity, bool reprocessRequested, bool hasActiveGenericModel, DateTimeOffset now, bool modelChanged = false)
    {
        if (entity.Status != JobStatus.Failed && !(entity.Status == JobStatus.Completed && (reprocessRequested || modelChanged)))
            return Reject(Rejection.InvalidTransition, "A new run requires Failed, or Completed with reprocess requested or changed models/mapping.");
        // SQL RunNo is smallint. No wraparound or accidental new billing after exhaustion.
        if (entity.CurrentRun.RunNo >= short.MaxValue)
            return Reject(Rejection.RunLimitReached, "RunNo exceeds SQL smallint.");
        return Decision<JobSnapshot>.Accept(Create(entity.RequestedShippingLineCode, hasActiveGenericModel, now)with { CurrentRun = new(entity.CurrentRun.RunNo + 1, entity.Status == JobStatus.Failed ? RunReason.Requeue : reprocessRequested ? RunReason.Reprocess : RunReason.ModelChanged, RunStatus.Active, now) });
    }

    public Decision<JobSnapshot> Claim(JobSnapshot entity, Guid newToken, DateTimeOffset now)
    {
        if (newToken == Guid.Empty || newToken == entity.LeaseToken)
            return Reject(Rejection.InvalidInput, "Every claim requires a new nonempty token.");
        if (!(entity.Status == JobStatus.Queued && entity.NextAttemptAtUtc <= now) && !(entity.Status == JobStatus.Processing && entity.LeaseExpiresAtUtc < now))
            return Reject(Rejection.NotDue, "Only a due Queued job or expired Processing lease can be claimed.");
        // [S3] claims first; Application must execute [S9] before doing work on the third takeover.
        return Decision<JobSnapshot>.Accept(entity with { Status = JobStatus.Processing, LeaseToken = newToken, LeaseExpiresAtUtc = now.AddSeconds(60), TakeoverCount = entity.TakeoverCount + (entity.Status == JobStatus.Processing ? 1 : 0) });
    }

    public Decision<JobSnapshot> Renew(JobSnapshot entity, Guid token, DateTimeOffset now) => Owns(entity, token) ? Decision<JobSnapshot>.Accept(entity with { LeaseExpiresAtUtc = now.AddSeconds(60) }) : Lost();
    public Decision<JobSnapshot> Release(JobSnapshot entity, Guid token, DateTimeOffset now, TimeSpan delay, string? errorCode = null)
    {
        if (!Owns(entity, token))
            return Lost();
        if (delay < TimeSpan.Zero)
            return Reject(Rejection.InvalidInput, "Release delay cannot be negative.");
        return Decision<JobSnapshot>.Accept(entity with { Status = JobStatus.Queued, LeaseToken = null, LeaseExpiresAtUtc = null, NextAttemptAtUtc = now + delay, Error = errorCode is null ? null : new(errorCode, entity.Error?.Message) });
    }

    public Decision<JobSnapshot> Retry(JobSnapshot entity, Guid token, DateTimeOffset now, TimeSpan? retryAfter = null, string? errorCode = null)
    {
        if (!Owns(entity, token))
            return Lost();
        var decision = _retryPolicyService.Failure(entity.Attempts, retryAfter);
        if (!decision.IsSuccess)
            return Reject(decision.Rejection!.Value, decision.Detail!);
        var retry = decision.Value!;
        if (retry.Action == RetryAction.Fail)
            return Fail(entity, token, new(ErrorCodes.RetriesExhausted, "Five retryable failures."), now, true);
        return Decision<JobSnapshot>.Accept(Release(entity, token, now, retry.Delay, errorCode).Value!with { Attempts = retry.Attempts });
    }

    public Decision<JobSnapshot> Fail(JobSnapshot entity, Guid token, DomainError error, DateTimeOffset now, bool countsAsAttempt = false, long? operationId = null)
    {
        if (!Owns(entity, token))
            return Lost();
        if (countsAsAttempt && entity.Attempts >= 5)
            return Reject(Rejection.InvalidInput, "Attempts cannot exceed five.");
        var operations = entity.Operations;
        var index = operations.FindIndex(o => o.OperationId == operationId && o.State == AzureOperationState.Accepted);
        if (index >= 0)
            operations = operations.SetItem(index, _azureOperationLifecycleService.Transition(operations[index], AzureOperationState.Failed, now).Value!);
        return Decision<JobSnapshot>.Accept(entity with { Status = JobStatus.Failed, CurrentRun = _jobRunLifecycleService.Finish(entity.CurrentRun, RunStatus.Failed, now, error).Value!, Attempts = entity.Attempts + (countsAsAttempt ? 1 : 0), Error = error, CompletedAtUtc = now, LeaseToken = null, LeaseExpiresAtUtc = null, Operations = operations });
    }

    public Decision<JobSnapshot> EnforceTakeoverBudget(JobSnapshot entity, Guid token, DateTimeOffset now) => !Owns(entity, token) ? Lost() : _takeoverPolicyService.IsExhausted(entity.TakeoverCount) ? Fail(entity, token, new(ErrorCodes.WorkerCrashLoop, "Three expired-lease takeovers."), now) : Decision<JobSnapshot>.Accept(entity);
    public Decision<JobSnapshot> BeginOperation(JobSnapshot entity, Guid token, long operationId, OperationKind kind, string modelId, DateTimeOffset now)
    {
        if (!Owns(entity, token))
            return Lost();
        if (_takeoverPolicyService.IsExhausted(entity.TakeoverCount))
            return Reject(Rejection.InvalidTransition, "Fail WORKER_CRASH_LOOP before further work.");
        if ((entity.Stage, kind)is not ((JobStage.Classify, OperationKind.Classify) or (JobStage.Extract, OperationKind.Extract)))
            return Reject(Rejection.InvalidTransition, "Operation kind does not match the current stage.");
        var start = _azureOperationLifecycleService.Start(operationId, entity.CurrentRun.RunNo, kind, modelId, entity.Operations, now);
        return start.IsSuccess ? Decision<JobSnapshot>.Accept(entity with { Operations = entity.Operations.Add(start.Value!) }) : Reject(start.Rejection!.Value, start.Detail!);
    }

    /// <summary>[S4b] records the 202 fact without requiring a lease. A rejected late 202 must not reopen Unknown.</summary>
    public Decision<JobSnapshot> RecordAccepted(JobSnapshot entity, long operationId, string operationLocation, DateTimeOffset now) => UpdateOperation(entity, operationId, o => _azureOperationLifecycleService.Accept(o, operationLocation, now));
    public Decision<JobSnapshot> CloseSending(JobSnapshot entity, Guid token, long operationId, AzureOperationState state, DateTimeOffset now)
    {
        if (!Owns(entity, token))
            return Lost();
        if (state is not (AzureOperationState.Unknown or AzureOperationState.Failed))
            return Reject(Rejection.InvalidTransition, "[S4c] only closes Sending as Failed or Unknown.");
        return UpdateOperation(entity, operationId, o => o.State == AzureOperationState.Sending && o.OperationLocation is null ? _azureOperationLifecycleService.Transition(o, state, now) : Decision<AzureOperationSnapshot>.Reject(Rejection.OperationNotOpen, "202 may have been recorded: reread [S19]."));
    }

    /// <summary>[S18]: operation success (and its ledger fact) persists even if classification fails.</summary>
    public Decision<JobSnapshot> ClassifierSucceeded(JobSnapshot entity, Guid token, long operationId, string? mappedLine, double confidence, double minClassConfidence, DateTimeOffset now)
    {
        if (!Owns(entity, token))
            return Lost();
        if (entity.Stage != JobStage.Classify || !ValidConfidence(confidence) || !ValidConfidence(minClassConfidence))
            return Reject(Rejection.InvalidInput, "Expected Classify stage and confidence values in [0,1].");
        var changed = SucceedOperation(entity, operationId, OperationKind.Classify, now);
        if (!changed.IsSuccess)
            return changed;
        var job = changed.Value!;
        if (_classifierPolicyService.ResolveRoute("BL", mappedLine is null ? null : "BL", mappedLine, confidence, minClassConfidence)is null)
            return Fail(job, token, new(ErrorCodes.Unclassified, "Class is unmapped or below its confidence threshold."), now);
        return Decision<JobSnapshot>.Accept(job with { Stage = JobStage.Extract, ShippingLineCode = mappedLine, LeaseExpiresAtUtc = now.AddSeconds(60), Error = null });
    }

    /// <summary>[S8a]: caller saves the raw blob first; operation, ledger and Map stage commit together.</summary>
    public Decision<JobSnapshot> ExtractionSucceeded(JobSnapshot entity, Guid token, long operationId, string rawResultPath, DateTimeOffset now)
    {
        if (!Owns(entity, token))
            return Lost();
        if (entity.Stage != JobStage.Extract || string.IsNullOrWhiteSpace(rawResultPath))
            return Reject(Rejection.InvalidInput, "Expected Extract stage and a saved raw result path.");
        var changed = SucceedOperation(entity, operationId, OperationKind.Extract, now);
        if (!changed.IsSuccess)
            return changed;
        var job = changed.Value!;
        var index = job.Operations.FindIndex(o => o.OperationId == operationId);
        return Decision<JobSnapshot>.Accept(job with { Stage = JobStage.Map, ModelId = job.Operations[index].ModelId, Operations = job.Operations.SetItem(index, job.Operations[index] with { RawResultPath = rawResultPath }), LeaseExpiresAtUtc = now.AddSeconds(60) });
    }

    public Decision<JobSnapshot> Complete(JobSnapshot entity, Guid token, DateTimeOffset now)
    {
        if (!Owns(entity, token))
            return Lost();
        if (entity.Stage != JobStage.Map)
            return Reject(Rejection.InvalidTransition, "Completion requires the mapped result [S8].");
        return Decision<JobSnapshot>.Accept(entity with { Status = JobStatus.Completed, CurrentRun = _jobRunLifecycleService.Finish(entity.CurrentRun, RunStatus.Completed, now).Value!, CompletedAtUtc = now, LeaseToken = null, LeaseExpiresAtUtc = null, Error = null });
    }

    public ResumeAction Resume(JobSnapshot entity) => _resumePolicyService.Decide(entity.TakeoverCount, entity.Operations.Any(o => o.State == AzureOperationState.Accepted) ? "Accepted" : entity.Operations.Any(o => o.State == AzureOperationState.Sending) ? "Sending" : null, entity.Stage.ToString(), entity.Operations.Count(o => o.State == AzureOperationState.Unknown));
    private Decision<JobSnapshot> SucceedOperation(JobSnapshot entity, long id, OperationKind kind, DateTimeOffset now) => UpdateOperation(entity, id, o => o.Kind == kind ? _azureOperationLifecycleService.Transition(o, AzureOperationState.Succeeded, now) : Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidTransition, "Operation kind does not match stage."));
    private Decision<JobSnapshot> UpdateOperation(JobSnapshot entity, long id, Func<AzureOperationSnapshot, Decision<AzureOperationSnapshot>> update)
    {
        var index = entity.Operations.FindIndex(o => o.OperationId == id);
        if (index < 0)
            return Reject(Rejection.OperationNotOpen, "Operation not found in current run.");
        var changed = update(entity.Operations[index]);
        return changed.IsSuccess ? Decision<JobSnapshot>.Accept(entity with { Operations = entity.Operations.SetItem(index, changed.Value!) }) : Reject(changed.Rejection!.Value, changed.Detail!);
    }

    private bool Owns(JobSnapshot entity, Guid token) => _leasePolicyService.Check(entity.Status, entity.LeaseToken, token) == LeaseWriteResult.Applied;
    private bool ValidConfidence(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
    private Decision<JobSnapshot> Reject(Rejection rejection, string detail) => Decision<JobSnapshot>.Reject(rejection, detail);
    private Decision<JobSnapshot> Lost() => Reject(Rejection.LeaseLost, "Lease lost: stop and write nothing.");
}
