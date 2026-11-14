using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

// Test composition only: production has no monolithic store or forwarding facade.
internal sealed class SqlDocAiStore : IJobRepository, IJobQueue, IOperationStore, IWebhookOutbox, IModelCatalog, IRetention, IConsumerSource, IUsageReader, IModelThresholdSource, ICallbackSource, ISignalReader
{
    private readonly SqlExecution execution;
    private readonly SqlJobRepository JobRepository;
    private readonly SqlJobQueueRepository JobQueue;
    private readonly SqlOperationStoreRepository OperationStore;
    private readonly SqlWebhookOutboxRepository WebhookOutbox;
    private readonly SqlModelCatalogRepository ModelCatalog;
    private readonly SqlRetentionRepository Retention;
    private readonly SqlConsumerSourceRepository ConsumerSource;
    private readonly SqlUsageReaderRepository UsageReader;
    private readonly SqlModelThresholdSourceRepository ModelThresholdSource;
    private readonly SqlCallbackSourceRepository CallbackSource;
    private readonly SqlSignalReaderRepository SignalReader;
    internal SqlDocAiStore(ISqlConnectionFactory connections, SqlStatementCatalog sql, IOptions<DocAiSqlOptions> options,
        IOptions<AnalyzeResultMappingOptions>? mappingOptions = null, ILogger<SqlDocAiStore>? logger = null,
        IMappingProfileResolver? mappingResolver = null)
    {
        execution = new SqlExecution(connections, sql, options, logger);
        JobRepository = new SqlJobRepository(execution, connections, options, mappingResolver ?? new InMemoryMappingProfileResolver(mappingOptions ?? Options.Create(new AnalyzeResultMappingOptions())));
        JobQueue = new SqlJobQueueRepository(execution);
        OperationStore = new SqlOperationStoreRepository(execution);
        WebhookOutbox = new SqlWebhookOutboxRepository(execution);
        ModelCatalog = new SqlModelCatalogRepository(execution);
        Retention = new SqlRetentionRepository(execution);
        ConsumerSource = new SqlConsumerSourceRepository(execution);
        UsageReader = new SqlUsageReaderRepository(execution, new ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService.PageAllowanceStatusService());
        ModelThresholdSource = new SqlModelThresholdSourceRepository(execution);
        CallbackSource = new SqlCallbackSourceRepository(execution);
        SignalReader = new SqlSignalReaderRepository(execution);
    }
    private Dapper.CommandDefinition Command(string label, object? values, CancellationToken ct) => execution.Command(label, values, ct);
    public Task<int> DailyPagesAsync(short consumerId, CancellationToken cancellationToken) => JobRepository.DailyPagesAsync(consumerId, cancellationToken);
    public Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken cancellationToken) => JobRepository.IntakeAsync(request, cancellationToken);
    public Task<RunOutcome?> GetRunAsync(Guid jobId, short consumerId, short? runNo, CancellationToken cancellationToken) => JobRepository.GetRunAsync(jobId, consumerId, runNo, cancellationToken);
    public Task<RunPosition?> GetPositionAsync(Guid jobId, CancellationToken cancellationToken) => JobRepository.GetPositionAsync(jobId, cancellationToken);
    public Task<WriteOutcome> CompleteAsync(Completion request, CancellationToken cancellationToken) => JobRepository.CompleteAsync(request, cancellationToken);
    public Task<WriteOutcome> FailAsync(Failure request, CancellationToken cancellationToken) => JobRepository.FailAsync(request, cancellationToken);
    public Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess request, CancellationToken cancellationToken) => JobRepository.ClassifierSucceededAsync(request, cancellationToken);
    public Task<ClassMapping?> GetClassMappingAsync(string classifierModelId, string className, CancellationToken cancellationToken) => JobRepository.GetClassMappingAsync(classifierModelId, className, cancellationToken);
    public Task<ClaimedJob?> ClaimAsync(CancellationToken cancellationToken) => JobQueue.ClaimAsync(cancellationToken);
    public Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken cancellationToken) => JobQueue.RenewAsync(lease, cancellationToken);
    public Task<WriteOutcome> RetryAsync(Lease lease, int retryAfterSeconds, string? errorCode, CancellationToken cancellationToken) => JobQueue.RetryAsync(lease, retryAfterSeconds, errorCode, cancellationToken);
    public Task<WriteOutcome> ReleaseAsync(Lease lease, int delaySeconds, string? errorCode, CancellationToken cancellationToken) => JobQueue.ReleaseAsync(lease, delaySeconds, errorCode, cancellationToken);
    public Task<BeginOperationResult> BeginAsync(Lease lease, string kind, string modelId, CancellationToken cancellationToken) => OperationStore.BeginAsync(lease, kind, modelId, cancellationToken);
    public Task<bool> AcceptedAsync(long operationId, string operationLocation, CancellationToken cancellationToken) => OperationStore.AcceptedAsync(operationId, operationLocation, cancellationToken);
    public Task<WriteOutcome> CloseSendingAsync(long operationId, Guid leaseToken, string state, string? azureErrorCode, CancellationToken cancellationToken) => OperationStore.CloseSendingAsync(operationId, leaseToken, state, azureErrorCode, cancellationToken);
    public Task<WriteOutcome> ExtractionSucceededAsync(ExtractionSuccess request, CancellationToken cancellationToken) => OperationStore.ExtractionSucceededAsync(request, cancellationToken);
    public Task<IReadOnlyList<ClaimedDelivery>> ClaimDueAsync(CancellationToken cancellationToken) => WebhookOutbox.ClaimDueAsync(cancellationToken);
    public Task<WriteOutcome> RecordAttemptAsync(long deliveryId, Guid leaseToken, short? statusCode, string? error, CancellationToken cancellationToken) => WebhookOutbox.RecordAttemptAsync(deliveryId, leaseToken, statusCode, error, cancellationToken);
    public Task<int> MakeDueAsync(short consumerId, CancellationToken ct) => WebhookOutbox.MakeDueAsync(consumerId, ct);
    public Task<int> ReplayAsync(short consumerId, CancellationToken cancellationToken) => WebhookOutbox.ReplayAsync(consumerId, cancellationToken);
    public Task<EventContext?> GetEventContextAsync(Guid jobId, short runNo, CancellationToken cancellationToken) => WebhookOutbox.GetEventContextAsync(jobId, runNo, cancellationToken);
    public Task ActivateAsync(string newModelId, decimal minimumAccuracy, CancellationToken cancellationToken) => ModelCatalog.ActivateAsync(newModelId, minimumAccuracy, cancellationToken);
    public Task<IReadOnlyList<CatalogModel>> GetCatalogAsync(CancellationToken cancellationToken) => ModelCatalog.GetCatalogAsync(cancellationToken);
    public Task RunAsync(CancellationToken cancellationToken) => Retention.RunAsync(cancellationToken);
    public Task<IReadOnlyList<Consumer>> GetConsumersAsync(CancellationToken ct) => ConsumerSource.GetConsumersAsync(ct);
    public Task<TopUpCyclesResponse?> GetTopUpCyclesAsync(short consumerId, long? grantId, CancellationToken ct) => UsageReader.GetTopUpCyclesAsync(consumerId, grantId, ct);
    public Task<CycleDocumentsResponse?> GetCycleDocumentsAsync(short consumerId, long grantId, int limit, long? afterChargeId, CancellationToken ct) => UsageReader.GetCycleDocumentsAsync(consumerId, grantId, limit, afterChargeId, ct);
    public Task<UsageResponse> GetUsageAsync(short consumerId, int months, DateTimeOffset asOfUtc, CancellationToken ct) => UsageReader.GetUsageAsync(consumerId, months, asOfUtc, ct);
    public Task<decimal?> GetThresholdAsync(string modelId, CancellationToken ct) => ModelThresholdSource.GetThresholdAsync(modelId, ct);
    public Task<IReadOnlyList<CallbackConsumer>> GetCallbacksAsync(CancellationToken ct) => CallbackSource.GetCallbacksAsync(ct);
    public Task<int?> OldestDueSecondsAsync(CancellationToken ct) => SignalReader.OldestDueSecondsAsync(ct);
    public Task<IReadOnlyList<UnpricedUsage>> UnpricedUsageAsync(CancellationToken ct) => SignalReader.UnpricedUsageAsync(ct);
}
