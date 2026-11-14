namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

public sealed record UsageResponse(DateTimeOffset AsOfUtc, string Unit, string QuotaMode, string Status,
    long Granted, long Used, long Reserved, long Remaining, int WarningPercent, long? CycleBase,
    long? WarningAtPages, UsageTopUp? LastTopUp, int MaxPagesPerJob, int DailyCap,
    long SubmittedTodayPages, long ChargedTodayPages, string DayBoundary, IReadOnlyList<UsageMonth> Months);
public sealed record UsageTopUp(DateTimeOffset RecordedAtUtc, long Pages);
public sealed record UsageMonth(string Month, long ChargedPages, long ChargedDocuments);
