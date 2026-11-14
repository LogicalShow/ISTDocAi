using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlJobQueueRepository : IJobQueue
{
    private readonly SqlExecution execution;
    internal SqlJobQueueRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public Task<ClaimedJob?> ClaimAsync(CancellationToken cancellationToken) => execution.One<ClaimedJob>("S3", null, cancellationToken);

    public Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken cancellationToken) => execution.Write("S5", lease, true, cancellationToken);

    public Task<WriteOutcome> RetryAsync(Lease lease, int retryAfterSeconds, string? errorCode, CancellationToken cancellationToken) =>
        execution.Write("S6", new { lease.JobId, lease.LeaseToken, retryAfterSeconds, errorCode }, true, cancellationToken);

    public Task<WriteOutcome> ReleaseAsync(Lease lease, int delaySeconds, string? errorCode, CancellationToken cancellationToken) =>
        execution.Write("S7", new { lease.JobId, lease.LeaseToken, delaySeconds, errorCode }, false, cancellationToken);

}
