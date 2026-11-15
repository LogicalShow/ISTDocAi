using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlOperationStoreRepository : IOperationStore
{
    private readonly SqlExecution execution;
    internal SqlOperationStoreRepository(SqlExecution execution)
    {
        this.execution = execution;
    }

    public async Task<BeginOperationResult> BeginAsync(Lease lease, string kind, string modelId, CancellationToken cancellationToken)
    {
        try
        {
            var id = await execution.One<long?>("S4a", new { lease.JobId, lease.LeaseToken, kind, modelId }, cancellationToken);
            return new(id.HasValue ? WriteOutcome.Applied : WriteOutcome.LeaseLost, id);
        }
        catch (SqlException e) when (e.Number is 50010 or 50012 or 50013 or 50017)
        {
            return new(e.Number switch { 50017 => WriteOutcome.CancelRequested, 50013 => WriteOutcome.RetriesExhausted, 50012 => WriteOutcome.OperationNotOpen, _ => WriteOutcome.LeaseLost }, null);
        }
    }

    public async Task<bool> AcceptedAsync(long operationId, string operationLocation, CancellationToken cancellationToken) =>
        await execution.Execute("S4b", new { operationId, operationLocation }, cancellationToken) > 0;

    public Task<WriteOutcome> CloseSendingAsync(long operationId, Guid leaseToken, string state, string? azureErrorCode, CancellationToken cancellationToken) =>
        execution.Write("S4c", new { operationId, leaseToken, state, azureErrorCode }, true, cancellationToken);

    public Task<WriteOutcome> ExtractionSucceededAsync(ExtractionSuccess request, CancellationToken cancellationToken) => execution.Write("S8a", request, false, cancellationToken);

}
