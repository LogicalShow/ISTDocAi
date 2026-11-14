namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record RunPosition(short RunNo, string Stage, long? OperationId, string? Kind, string? ModelId,
    string? State, string? OperationLocation, DateTimeOffset? AcceptedAtUtc, int UnknownInRun,
    long? MapOperationId, string? MapRawResultPath, string? MapModelId = null)
{
    public string? RequestedDocumentTypeCode { get; init; }
    public string? ResolvedDocumentTypeCode { get; init; }
    public string? ShippingLineCode { get; init; }
    private RunPosition() : this(default, "", null, null, null, null, null, null, default, null, null) { }
}
