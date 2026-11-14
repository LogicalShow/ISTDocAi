using System.Data.Common;
using System.Diagnostics;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class RestrictedPackageConnectionTests
{
    private static SqlConnection Connection(RestrictedPackageConnections restriction,string database="Proof",bool pooling=false) =>
        new(new SqlConnectionStringBuilder { DataSource="invalid.invalid",InitialCatalog=database,
            ApplicationName=restriction.Marker,Pooling=pooling }.ConnectionString);

    [TestCase(false)]
    [TestCase(true)]
    public async Task Raw_EF_and_factory_callbacks_apply_the_same_identity_once_per_open(bool async)
    {
        var commands=new List<string>();
        using var restriction=new RestrictedPackageConnections("Proof","proof_user",(_,sql)=>commands.Add(sql));
        restriction.Start();
        using var listener=new DiagnosticListener("Stage14b.Offline.Restriction");
        using var connection=Connection(restriction);
        // Synthetic notifications only. No connection.Open or command execution occurs.
        listener.Write("Microsoft.Data.SqlClient.WriteConnectionOpenAfter",new { Connection=connection });
        if(async) await restriction.ConnectionOpenedAsync(connection,null!);
        else restriction.ConnectionOpened(connection,null!);
        var factory=new RestrictedPackageConnections.Factory(new UnopenedFactory(connection),restriction);
        var returned=async ? await factory.OpenAsync(default) : factory.Open();
        Assert.That(returned,Is.SameAs(connection));
        Assert.That(commands,Is.EqualTo(new[]{"EXECUTE AS USER = N'proof_user' WITH NO REVERT;"}));
        Assert.That(restriction.TargetOpens,Is.EqualTo(1));
        Assert.That(restriction.EfOpens,Is.EqualTo(1));
    }
    [Test]
    public void Master_exemption_allows_only_public_lock_and_database_probe()
    {
        var impersonations=0;
        using var restriction=new RestrictedPackageConnections("Proof","proof_user",(_,_)=>impersonations++);
        restriction.Start();
        using var listener=new DiagnosticListener("Stage14b.Offline.Master");
        using var master=Connection(restriction,"master");
        listener.Write("Microsoft.Data.SqlClient.WriteConnectionOpenAfter",new { Connection=master });
        using var command=new SqlCommand("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive',
                @LockOwner='Session', @LockTimeout=@timeout, @DbPrincipal='public';
            SELECT @result;
            """,master);
        listener.Write("Microsoft.Data.SqlClient.WriteCommandBefore",new { Command=command });
        command.CommandText="SELECT DB_ID(@name)";
        listener.Write("Microsoft.Data.SqlClient.WriteCommandBefore",new { Command=command });
        Assert.That(restriction.MasterLocks,Is.EqualTo(1));
        Assert.That(restriction.MasterProbes,Is.EqualTo(1));
        Assert.That(impersonations,Is.Zero);
        command.CommandText="CREATE DATABASE forbidden";
        Assert.Throws<InvalidOperationException>(()=>listener.Write("Microsoft.Data.SqlClient.WriteCommandBefore",new { Command=command }));
    }
    [Test]
    public void Pooling_and_other_database_are_refused_and_unmarked_DBA_connections_are_untouched()
    {
        var impersonations=0;
        using var restriction=new RestrictedPackageConnections("Proof","proof_user",(_,_)=>impersonations++);
        using var pooled=Connection(restriction,pooling:true);
        using var wrongDatabase=Connection(restriction,"AnotherDatabase");
        using var dba=new SqlConnection("Server=invalid.invalid;Database=Proof;Pooling=false");
        Assert.Throws<InvalidOperationException>(()=>restriction.Apply(pooled));
        Assert.Throws<InvalidOperationException>(()=>restriction.Apply(wrongDatabase));
        restriction.Apply(dba);
        Assert.Throws<InvalidOperationException>(()=>restriction.ConnectionOpened(dba,null!));
        var factory=new RestrictedPackageConnections.Factory(new UnopenedFactory(dba),restriction);
        Assert.Throws<InvalidOperationException>(()=>factory.Open());
        Assert.That(impersonations,Is.Zero);
    }
    private sealed class UnopenedFactory(SqlConnection connection) : ISqlConnectionFactory
    {
        public SqlConnection Open()=>connection;
        public Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)=>Task.FromResult(connection);
    }
}
