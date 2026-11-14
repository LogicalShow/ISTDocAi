using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Enums.DocAi;
public enum ErrorSource
{
    PostAnalyze,
    GetPoll,
    AnalysisFailed,
    OriginalMissing,
    Unclassified,
    ModelMissing,
    Timeout,
    Mapping,
    LeaseLost,
    Deadlock,
    Configuration
}
