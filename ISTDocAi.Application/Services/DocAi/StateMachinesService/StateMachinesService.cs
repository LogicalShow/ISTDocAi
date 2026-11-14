using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.StateMachinesService;
public sealed class StateMachinesService : IStateMachinesService
{
    // Terminal jobs can only return to Queued through JobSnapshot.StartNewRun, never a worker transition.
    public bool CanTransition(JobStatus from, JobStatus to) => (from, to)is (JobStatus.Queued, JobStatus.Processing) or (JobStatus.Processing, JobStatus.Queued or JobStatus.Completed or JobStatus.Failed);
    public bool CanTransition(RunStatus from, RunStatus to) => from == RunStatus.Active && to is RunStatus.Completed or RunStatus.Failed;
    public bool CanTransition(JobStage from, JobStage to) => (from, to)is (JobStage.Classify, JobStage.Extract) or (JobStage.Extract, JobStage.Map);
    public bool CanTransition(AzureOperationState from, AzureOperationState to) => (from, to)is (AzureOperationState.Sending, AzureOperationState.Accepted or AzureOperationState.Failed or AzureOperationState.Unknown) or (AzureOperationState.Accepted, AzureOperationState.Succeeded or AzureOperationState.Failed);
    public bool CanTransition(WebhookDeliveryStatus from, WebhookDeliveryStatus to, bool currentRun = false) => (from == WebhookDeliveryStatus.Pending && to is WebhookDeliveryStatus.Delivered or WebhookDeliveryStatus.DeadLettered or WebhookDeliveryStatus.Superseded) || (from == WebhookDeliveryStatus.DeadLettered && to == WebhookDeliveryStatus.Pending && currentRun);
    public JobStage StartingStage(string? requestedShippingLineCode, bool hasActiveGenericModel) => requestedShippingLineCode is not null || hasActiveGenericModel ? JobStage.Extract : JobStage.Classify;
}
