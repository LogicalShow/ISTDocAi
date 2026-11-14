using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;
namespace ISTDocAi.UI.Server.Tests.DocAi;
internal sealed class CapturedRequestLogs : IRequestLogService
{
    public List<string> Payloads { get; } = [];
    public Task Log(string requestType, string requestParameters, DateTime requestInitTime, DateTime requestCompleteTime, string? responseError, CancellationToken cancellationToken)
    { Payloads.Add(requestParameters); return Task.CompletedTask; }
}
internal sealed class ForbiddenUnitOfWork : IUnitOfWork
{
    public void Dispose() { }
    public void ClearTrackedEntities(CancellationToken ct) => throw new InvalidOperationException("Unexpected IAM write");
    public Task BeginTransactionAsync(CancellationToken ct) => throw new InvalidOperationException();
    public Task CommitTransactionAsync(CancellationToken ct) => throw new InvalidOperationException();
    public Task RollbackTransactionAsync(CancellationToken ct) => throw new InvalidOperationException();
    public Task<int> SaveChangesAsync(CancellationToken ct) => throw new InvalidOperationException();
}

internal sealed class CapturedPayloadSerializer : ISTDocAi.Application.Common.Interfaces.Serialization.ILogPayloadSerializer
{
    public int Calls { get; private set; }
    public string Serialize<T>(T value) { Calls++; return "FORBIDDEN_PAYLOAD"; }
}

internal sealed class DenySql : ISTDocAi.Infrastructure.Data.Sql.DocAi.ISqlConnectionFactory
{
    public Task<Microsoft.Data.SqlClient.SqlConnection> OpenAsync(CancellationToken ct) => throw new InvalidOperationException("SQL prohibited in offline HTTP tests");
}
internal sealed class DenyHttp : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => throw new InvalidOperationException("External HTTP prohibited in offline tests");
}

internal sealed class CapturedSerilogSink : Serilog.Core.ILogEventSink
{
    public List<Serilog.Events.LogEvent> Events { get; } = [];
    public void Emit(Serilog.Events.LogEvent entry) => Events.Add(entry);
}
