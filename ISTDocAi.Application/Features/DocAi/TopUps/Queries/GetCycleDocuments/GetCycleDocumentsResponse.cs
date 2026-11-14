using V1 = ISTDocAi.Application.Common.Contracts.DocAi.V1;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetCycleDocuments;
public sealed record GetCycleDocumentsResponse(long? TopUpGrantId, IReadOnlyList<GetCycleDocumentsChargedRun> Documents, long? NextAfterChargeId)
{
    public static GetCycleDocumentsResponse From(V1.CycleDocumentsResponse b) => new(b.TopUpGrantId, b.Documents.Select(GetCycleDocumentsChargedRun.From).ToArray(), b.NextAfterChargeId);
}
public sealed record GetCycleDocumentsChargedRun(long ChargeId, Guid JobId, short RunNo, long Pages, DateTimeOffset ChargedAtUtc,
    string? ResolvedDocumentTypeCode, string RunReason)
{
    public static GetCycleDocumentsChargedRun From(V1.ChargedRun b) => new(b.ChargeId, b.JobId, b.RunNo, b.Pages, b.ChargedAtUtc, b.ResolvedDocumentTypeCode, b.RunReason);
}
