using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Enums.DocAi;
public enum RetryAction
{
    Requeue,
    Fail,
    Release,
    Stop,
    Wait
}
