namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

public sealed record TopUpCyclesResponse(string OrderingPolicy, string Coverage, AllowanceTotals Totals,
    TopUpCycle? Prelude, IReadOnlyList<TopUpCycle> Cycles, bool HasMoreCycles);
public sealed record AllowanceTotals(long GrantedPages, long ChargedPages, long ChargedRuns, long LedgerBalance,
    long CurrentReservedPages, long CurrentAvailablePages);
public sealed record TopUpCycle(long? TopUpGrantId, DateTimeOffset? StartAtUtc, long? NextTopUpGrantId,
    DateTimeOffset? EndAtUtc, long TopUpPages, bool IsReversed, long? RecordedAvailableAfterTopUp,
    long OpeningLedgerBalance, long AdjustmentPages, long ReversalPages, long ChargedPages,
    long ChargedRuns, long ClosingLedgerBalance);
public sealed record CycleDocumentsResponse(long? TopUpGrantId, IReadOnlyList<ChargedRun> Documents, long? NextAfterChargeId);
public sealed record ChargedRun(long ChargeId, Guid JobId, short RunNo, long Pages, DateTimeOffset ChargedAtUtc,
    string? ResolvedDocumentTypeCode, string RunReason);
