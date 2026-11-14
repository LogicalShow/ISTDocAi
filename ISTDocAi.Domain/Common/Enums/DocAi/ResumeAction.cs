using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Enums.DocAi;
public enum ResumeAction
{
    Submit,
    Poll,
    CloseUnknown,
    Map,
    FailRetriesExhausted,
    FailCrashLoop
}
