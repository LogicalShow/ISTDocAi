using V1 = ISTDocAi.Application.Common.Contracts.DocAi.V1;
namespace ISTDocAi.Application.Features.DocAi.Usage.Queries.GetUsage;
public sealed record GetUsageResponse(DateTimeOffset AsOfUtc, string Unit, string QuotaMode, string Status,
    long Granted, long Used, long Reserved, long Remaining, int WarningPercent, long? CycleBase,
    long? WarningAtPages, GetUsageUsageTopUp? LastTopUp, int MaxPagesPerJob, int DailyCap,
    long SubmittedTodayPages, long ChargedTodayPages, string DayBoundary, IReadOnlyList<GetUsageUsageMonth> Months)
{
    public static GetUsageResponse From(V1.UsageResponse b) => new(b.AsOfUtc, b.Unit, b.QuotaMode, b.Status, b.Granted, b.Used, b.Reserved, b.Remaining, b.WarningPercent, b.CycleBase, b.WarningAtPages, b.LastTopUp is null ? null : GetUsageUsageTopUp.From(b.LastTopUp), b.MaxPagesPerJob, b.DailyCap, b.SubmittedTodayPages, b.ChargedTodayPages, b.DayBoundary, b.Months.Select(GetUsageUsageMonth.From).ToArray());
}
public sealed record GetUsageUsageTopUp(DateTimeOffset RecordedAtUtc, long Pages)
{
    public static GetUsageUsageTopUp From(V1.UsageTopUp b) => new(b.RecordedAtUtc, b.Pages);
}
public sealed record GetUsageUsageMonth(string Month, long ChargedPages, long ChargedDocuments)
{
    public static GetUsageUsageMonth From(V1.UsageMonth b) => new(b.Month, b.ChargedPages, b.ChargedDocuments);
}
