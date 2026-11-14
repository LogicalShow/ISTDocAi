namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public static class AlertEvents
{
    public const string IdentityCertificateExpiring = "identity.certificate_expiring";
    public const string QueueStuck = "queue.stuck";
    public const string BreakerOpened = "breaker.opened";
    public const string UsageUnpriced = "usage.unpriced";
    public const string JobFailed = "job.failed";
    public const string JobCompleted = "job.completed";
    public const string WebhookDeadLettered = "webhook.dead_lettered";
    public const string RegistryDrift = "registry.drift";
    public const string AnalysisResultMissing = "analysis.result_missing";
    public const string JobTakenOver = "job.taken_over";
    public const string OperationUnknown = "operation.unknown";
}
