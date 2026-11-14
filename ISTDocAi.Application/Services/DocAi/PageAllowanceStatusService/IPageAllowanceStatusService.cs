using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService;
public interface IPageAllowanceStatusService
{
    (string Status, long? WarningAtPages) Calculate(string quotaMode, long remaining, long? cycleBase, int warningPercent);
}
