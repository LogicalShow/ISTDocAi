namespace ISTDocAi.UI.Server.Idempotency;

internal sealed record IdempotencyCacheEntry(
    int StatusCode,
    string ContentType,
    string Body);
