namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record CallbackConsumer(short ConsumerId, string Code, string CallbackUrl,
    string WebhookSecretName, string? CallbackCaCertName);
