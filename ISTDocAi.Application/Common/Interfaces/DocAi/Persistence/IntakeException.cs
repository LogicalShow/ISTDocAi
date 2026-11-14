namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed class IntakeException(int status, string code, string title, string detail, int? retryAfter = null, long? requiredPages = null, long? availablePages = null) : Exception(detail)
{
    public long? RequiredPages { get; } = requiredPages;
    public long? AvailablePages { get; } = availablePages;
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public int? RetryAfter { get; } = retryAfter;
    public static IntakeException Invalid() => new(400, "INVALID_REQUEST", "Invalid request", "The submitted fields or document are invalid.");
}
