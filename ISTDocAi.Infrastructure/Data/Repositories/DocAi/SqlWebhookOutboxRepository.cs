using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlWebhookOutboxRepository : IWebhookOutbox, IConsumerWebhookRetry
{
    private readonly SqlExecution execution;
    internal SqlWebhookOutboxRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<IReadOnlyList<ClaimedDelivery>> ClaimDueAsync(CancellationToken cancellationToken) => execution.Many<ClaimedDelivery>("S10", null, cancellationToken);

    public Task<WriteOutcome> RecordAttemptAsync(long deliveryId, Guid leaseToken, short? statusCode, string? error, CancellationToken cancellationToken) =>
        execution.Write("S11", new { deliveryId, leaseToken, statusCode, error }, false, cancellationToken);

    public Task<int> MakeDueAsync(short consumerId, CancellationToken cancellationToken) =>
        execution.Execute("S31", new { consumerId }, cancellationToken);

    public Task<int> ReplayAsync(short consumerId, CancellationToken cancellationToken) => execution.Execute("S12", new { consumerId }, cancellationToken);

    public Task<EventContext?> GetEventContextAsync(Guid jobId, short runNo, CancellationToken cancellationToken) => execution.One<EventContext>("S17", new { jobId, runNo }, cancellationToken);

}
