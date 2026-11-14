namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record RunOutcome(Guid JobId, short RunNo, string? ClientRequestId, string Status, string? ErrorCode,
    string? ErrorMessage, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc, short PageCount,
    string? ContractVersion, string? ResultJson, short? FlaggedFields)
{
    public string RunReason { get; init; } = "New";
    public string? ChangesJson { get; init; }
    public string? RequestedDocumentTypeCode { get; init; }
    public string? ResolvedDocumentTypeCode { get; init; }
    public string? ResultType { get; init; }
    private RunOutcome() : this(default, default, null, "", null, null, default, null, default, null, null, null) { }
}
