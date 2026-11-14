using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Enums.DocAi;
public enum Rejection
{
    InvalidTransition,
    InvalidInput,
    LeaseLost,
    NotDue,
    OperationNotOpen,
    OpenOperationExists,
    RetriesExhausted,
    RunLimitReached
}
