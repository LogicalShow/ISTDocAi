using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Application.Services.DocAi.StateMachinesService;

namespace ISTDocAi.Application.Services.DocAi.AzureOperationLifecycleService;
public sealed class AzureOperationLifecycleService(IStateMachinesService _stateMachinesService) : IAzureOperationLifecycleService
{
    /// <summary>Existing operations must belong to this job, including history if available.
    /// The caller supplies the database-assigned id returned by [S4a].
    /// [S4a] counts Unknown across both kinds in the current run.</summary>
    public Decision<AzureOperationSnapshot> Start(long id, int runNo, OperationKind kind, string modelId, IEnumerable<AzureOperationSnapshot> existing, DateTimeOffset now)
    {
        if (existing is null || id <= 0 || runNo is < 1 or > short.MaxValue || !Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(modelId))
            return Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidInput, "Expected an id, SQL run number, kind and model.");
        var operations = existing.ToArray();
        if (operations.Any(o => o.OperationId == id))
            return Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidInput, "Operation id already exists.");
        if (operations.Count(o => o.RunNo == runNo && o.State == AzureOperationState.Unknown) >= 2)
            return Decision<AzureOperationSnapshot>.Reject(Rejection.RetriesExhausted, ErrorCodes.RetriesExhausted);
        if (operations.Any(o => o.RunNo == runNo && o.Kind == kind && o.State is AzureOperationState.Sending or AzureOperationState.Accepted))
            return Decision<AzureOperationSnapshot>.Reject(Rejection.OpenOperationExists, "Only one open operation per job, run and kind.");
        return Decision<AzureOperationSnapshot>.Accept(new() { OperationId = id, RunNo = runNo, Kind = kind, ModelId = modelId, State = AzureOperationState.Sending, CreatedAtUtc = now });
    }

    public Decision<AzureOperationSnapshot> Accept(AzureOperationSnapshot entity, string operationLocation, DateTimeOffset now) => string.IsNullOrWhiteSpace(operationLocation) ? Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidInput, "202 must include Operation-Location.") : entity.State != AzureOperationState.Sending ? Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidTransition, "Only Sending can record 202.") : Decision<AzureOperationSnapshot>.Accept(entity with { State = AzureOperationState.Accepted, OperationLocation = operationLocation, AcceptedAtUtc = now });
    /// <summary>Acceptance uses Accept so its location cannot be omitted.</summary>
    public Decision<AzureOperationSnapshot> Transition(AzureOperationSnapshot entity, AzureOperationState state, DateTimeOffset now) => !_stateMachinesService.CanTransition(entity.State, state) || state == AzureOperationState.Accepted ? Decision<AzureOperationSnapshot>.Reject(Rejection.InvalidTransition, $"Cannot change {entity.State} to {state}; acceptance requires a location.") : Decision<AzureOperationSnapshot>.Accept(entity with { State = state, CompletedAtUtc = now });
}
