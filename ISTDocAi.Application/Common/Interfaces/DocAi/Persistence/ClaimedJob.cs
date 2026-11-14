namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record ClaimedJob(Guid JobId, short RunNo, Guid LeaseToken, byte TakeoverCount, short ConsumerId,
    string DocumentTypeCode, string? ShippingLineCode, string Stage, string BlobPath, short PageCount, byte Attempts, string CorrelationId)
{
    public string? ResolvedDocumentTypeCode { get; init; }
    // Dapper uses this constructor for the expanded SQL projection; existing callers keep theirs.
    private ClaimedJob() : this(default, default, default, default, default, "", null, "", "", default, default, "") { }
}
