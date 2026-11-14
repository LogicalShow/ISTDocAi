using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ISTDocAi.UI.Server.Tests.DocAi;

// Test-only: the marker excludes DBA setup/catalog/cleanup connections from impersonation.
// The SqlClient observer also covers LockedDatabaseSequence's directly constructed connections
// and DocAiDbContext, which cannot receive a DI-registered EF interceptor.
internal sealed class RestrictedPackageConnections(string database, string principal, Action<DbConnection,string>? impersonate=null)
    : DbConnectionInterceptor, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string,object?>>, IDisposable
{
    private readonly List<IDisposable> subscriptions=[];
    private readonly ConditionalWeakTable<DbConnection, OpenState> states=new();
    private IDisposable? allListeners;
    private int targetOpens;
    private int efOpens;
    private int masterLocks;
    private int masterProbes;
    internal const string MasterLockSql="DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=@timeout, @DbPrincipal='public'; SELECT @result;";
    internal string Marker => "ISTDocAiRestricted:"+principal;
    internal int TargetOpens => targetOpens;
    internal int EfOpens => efOpens;
    internal int MasterLocks => masterLocks;
    internal int MasterProbes => masterProbes;
    internal void Start() => allListeners=DiagnosticListener.AllListeners.Subscribe(this);
    public void OnNext(DiagnosticListener listener)
    {
        lock(subscriptions) subscriptions.Add(listener.Subscribe(this, name =>
            name=="Microsoft.Data.SqlClient.WriteConnectionOpenAfter" || name=="Microsoft.Data.SqlClient.WriteCommandBefore"));
    }
    public void OnNext(KeyValuePair<string,object?> item)
    {
        if(item.Key=="Microsoft.Data.SqlClient.WriteConnectionOpenAfter") {
            if(item.Value?.GetType().GetProperty("Connection")?.GetValue(item.Value) is DbConnection connection) Apply(connection);
        } else if(item.Key=="Microsoft.Data.SqlClient.WriteCommandBefore" &&
                  item.Value?.GetType().GetProperty("Command")?.GetValue(item.Value) is DbCommand command &&
                  command.Connection is { } opened && Matches(opened)) {
            if(!string.Equals(opened.Database,"master",StringComparison.OrdinalIgnoreCase)) { Apply(opened); return; }
            // Closed allowlist: only these two public master operations are exempt.
            if(string.Join(" ",command.CommandText.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))==MasterLockSql)
                Interlocked.Increment(ref masterLocks);
            else if(command.CommandText=="SELECT DB_ID(@name)") Interlocked.Increment(ref masterProbes);
            else throw new InvalidOperationException("Unexpected statement outside the restricted database principal.");
        }
    }
    private bool Matches(DbConnection connection) =>
        new SqlConnectionStringBuilder(connection.ConnectionString).ApplicationName==Marker;
    internal void Apply(DbConnection connection)
    {
        if(!Matches(connection)) return;
        if(new SqlConnectionStringBuilder(connection.ConnectionString).Pooling)
            throw new InvalidOperationException("Restricted proof requires pooling disabled.");
        if(string.Equals(connection.Database,"master",StringComparison.OrdinalIgnoreCase)) return;
        if(!string.Equals(connection.Database,database,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restricted proof opened an unexpected database.");
        var state=states.GetValue(connection,c=> {
            var value=new OpenState();
            c.StateChange+=(_,e)=> { if(e.CurrentState==ConnectionState.Closed) value.Applied=false; };
            return value;
        });
        lock(state) {
            if(state.Applied || state.Applying) return; // Do not recursively intercept EXECUTE AS itself.
            state.Applying=true;
            try {
                var sql="EXECUTE AS USER = N'"+principal.Replace("'","''")+"' WITH NO REVERT;";
                if(impersonate is not null) impersonate(connection,sql); // Offline observer-routing tests only.
                else {
                    using var command=connection.CreateCommand();
                    command.CommandText=sql;
                    command.ExecuteNonQuery();
                }
                state.Applied=true;
                Interlocked.Increment(ref targetOpens);
            } finally { state.Applying=false; }
        }
    }
    private void ApplyApplication(DbConnection connection)
    {
        if(!Matches(connection)) throw new InvalidOperationException("Application proof connection lost its restriction marker.");
        Apply(connection);
    }
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    { ApplyApplication(connection); Interlocked.Increment(ref efOpens); }
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken=default)
    { ApplyApplication(connection); Interlocked.Increment(ref efOpens); return Task.CompletedTask; }
    public void OnCompleted() { }
    public void OnError(Exception error) => throw new InvalidOperationException("Restricted connection observation failed.");
    public void Dispose()
    {
        allListeners?.Dispose();
        lock(subscriptions) { foreach(var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear(); }
    }
    private sealed class OpenState { internal bool Applied; internal bool Applying; }

    internal sealed class Factory(ISqlConnectionFactory inner, RestrictedPackageConnections restriction) : ISqlConnectionFactory
    {
        public SqlConnection Open()
        {
            var connection=inner.Open();
            try { restriction.ApplyApplication(connection); return connection; } catch { connection.Dispose(); throw; }
        }
        public async Task<SqlConnection> OpenAsync(CancellationToken token)
        {
            var connection=await inner.OpenAsync(token);
            try { restriction.ApplyApplication(connection); return connection; } catch { await connection.DisposeAsync(); throw; }
        }
    }
}
