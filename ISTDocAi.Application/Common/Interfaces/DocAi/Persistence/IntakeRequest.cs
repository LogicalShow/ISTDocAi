namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record IntakeRequest(Guid JobId, short ConsumerId, string? ClientRequestId, string DocumentTypeCode,
    string? RequestedLine, byte[] FileSha256, string? FileName, string ContentType, int FileSizeBytes,
    short PageCount, string? BlobPath, string CorrelationId, bool Reprocess = false)
{
    public string IdemLine => RequestedLine ?? "*";
    public short? ExpectedRunNo { get; init; }
    public string? ExpectedDocumentType { get; init; }
    public string? ExpectedLine { get; init; }
    public string? ActiveClassifierModelId { get; init; }
    public string? ActiveExtractModelId { get; init; }
    public string? ActiveMappingFingerprint { get; init; }
}
