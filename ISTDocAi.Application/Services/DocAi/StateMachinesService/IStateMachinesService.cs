using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.StateMachinesService;
public interface IStateMachinesService
{
    bool CanTransition(JobStatus from, JobStatus to);
    bool CanTransition(RunStatus from, RunStatus to);
    bool CanTransition(JobStage from, JobStage to);
    bool CanTransition(AzureOperationState from, AzureOperationState to);
    bool CanTransition(WebhookDeliveryStatus from, WebhookDeliveryStatus to, bool currentRun = false);
    JobStage StartingStage(string? requestedShippingLineCode, bool hasActiveGenericModel);
}
