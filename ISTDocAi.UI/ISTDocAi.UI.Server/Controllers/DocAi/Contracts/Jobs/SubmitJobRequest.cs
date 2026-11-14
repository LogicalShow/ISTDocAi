namespace ISTDocAi.UI.Server.Controllers.DocAi.Contracts.Jobs;
// Streamed multipart contract; parsed exclusively by MultipartUpload, never by MVC form binding.
public sealed record SubmitJobRequest(string DocumentType, string? ShippingLine, string ClientRequestId, bool Reprocess)
{
    public const int MaxFileBytes = 20 * 1024 * 1024;
    public const int MaxRequestBytes = MaxFileBytes + 64 * 1024;
}
