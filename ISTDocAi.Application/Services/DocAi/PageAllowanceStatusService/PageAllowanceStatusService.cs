using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService;
public sealed class PageAllowanceStatusService : IPageAllowanceStatusService
{
    public (string Status, long? WarningAtPages) Calculate(string quotaMode, long remaining, long? cycleBase, int warningPercent)
    {
        // Decimal avoids bigint overflow and preserves floor at large balances.
        long? threshold = cycleBase is { } basis ? (long)decimal.Floor((decimal)basis * warningPercent / 100) : null;
        if (cycleBase is> 0)
            threshold = Math.Max(1, threshold!.Value);
        return (quotaMode == "Unlimited" ? "Unlimited" : remaining <= 0 ? "Exhausted" : threshold is { } warning && remaining <= warning ? "Low" : "Ok", threshold);
    }
}
