using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;

namespace ISTDocAi.Application.Services.DocAi.AzureOperationLifecycleService;
public interface IAzureOperationLifecycleService
{
    Decision<AzureOperationSnapshot> Start(long id, int runNo, OperationKind kind, string modelId, IEnumerable<AzureOperationSnapshot> existing, DateTimeOffset now);
    Decision<AzureOperationSnapshot> Accept(AzureOperationSnapshot entity, string operationLocation, DateTimeOffset now);
    Decision<AzureOperationSnapshot> Transition(AzureOperationSnapshot entity, AzureOperationState state, DateTimeOffset now);
}
