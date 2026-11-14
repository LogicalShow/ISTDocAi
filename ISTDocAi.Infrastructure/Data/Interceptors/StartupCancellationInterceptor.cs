using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace ISTDocAi.Infrastructure.Data.Interceptors;

/// <summary>Only active during startup; cancels seed commands, including Identity's commands.</summary>
public sealed class StartupCancellationInterceptor : DbCommandInterceptor
{
    public CancellationToken Token { get; set; }
    private void Attach(DbCommand command)
    {
        Token.ThrowIfCancellationRequested();
        if (Token.CanBeCanceled) Token.Register(() => { try { command.Cancel(); } catch (Exception) { } });
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Attach(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Attach(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Attach(command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Attach(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { Attach(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Attach(command); return ValueTask.FromResult(result); }
}
