using V1 = ISTDocAi.Application.Common.Contracts.DocAi.V1;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetTopUpCycles;
public sealed record GetTopUpCyclesResponse(string OrderingPolicy, string Coverage, GetTopUpCyclesAllowanceTotals Totals,
    GetTopUpCyclesTopUpCycle? Prelude, IReadOnlyList<GetTopUpCyclesTopUpCycle> Cycles, bool HasMoreCycles)
{
    public static GetTopUpCyclesResponse From(V1.TopUpCyclesResponse b) => new(b.OrderingPolicy, b.Coverage, GetTopUpCyclesAllowanceTotals.From(b.Totals), b.Prelude is null ? null : GetTopUpCyclesTopUpCycle.From(b.Prelude), b.Cycles.Select(GetTopUpCyclesTopUpCycle.From).ToArray(), b.HasMoreCycles);
}
public sealed record GetTopUpCyclesAllowanceTotals(long GrantedPages, long ChargedPages, long ChargedRuns, long LedgerBalance,
    long CurrentReservedPages, long CurrentAvailablePages)
{
    public static GetTopUpCyclesAllowanceTotals From(V1.AllowanceTotals b) => new(b.GrantedPages, b.ChargedPages, b.ChargedRuns, b.LedgerBalance, b.CurrentReservedPages, b.CurrentAvailablePages);
}
public sealed record GetTopUpCyclesTopUpCycle(long? TopUpGrantId, DateTimeOffset? StartAtUtc, long? NextTopUpGrantId,
    DateTimeOffset? EndAtUtc, long TopUpPages, bool IsReversed, long? RecordedAvailableAfterTopUp,
    long OpeningLedgerBalance, long AdjustmentPages, long ReversalPages, long ChargedPages,
    long ChargedRuns, long ClosingLedgerBalance)
{
    public static GetTopUpCyclesTopUpCycle From(V1.TopUpCycle b) => new(b.TopUpGrantId, b.StartAtUtc, b.NextTopUpGrantId, b.EndAtUtc, b.TopUpPages, b.IsReversed, b.RecordedAvailableAfterTopUp, b.OpeningLedgerBalance, b.AdjustmentPages, b.ReversalPages, b.ChargedPages, b.ChargedRuns, b.ClosingLedgerBalance);
}
