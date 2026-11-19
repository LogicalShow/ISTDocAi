using ISTDocAi.Infrastructure.Migrations.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
public sealed class RemoveDocAiRolesSqlTests
{
    private const string Roles = "SELECT COUNT(*) FROM sys.database_principals WHERE name IN ('docai_runtime','docai_operator','docai_quota_operator','docai_modelteam')";

    [Test]
    public async Task Fresh_migrations_remove_roles_and_dbo_cannot_update_or_delete_accounting_rows()
    {
        SqlTestGuard.Require();
        var db = new SqlDatabase();
        try
        {
            await db.InitializeAsync();
            Assert.That(await db.Scalar<int>(Roles), Is.Zero);
            // Triggers fire even for empty sets: these attempts must fail as dbo, independently of permissions.
            foreach (var table in new[] { "PageAllowanceGrants", "CustomerPageCharges" })
            foreach (var command in new[] { $"UPDATE dbo.{table} SET ConsumerId=ConsumerId", $"DELETE dbo.{table}" })
            {
                var error = Assert.ThrowsAsync<SqlException>(() => db.Execute(command));
                Assert.That(error!.Number, Is.EqualTo(50017), command);
            }
        }
        finally { await db.DisposeAsync(); }
    }

    [Test]
    public async Task Upgrade_removes_members_transfers_ownership_and_replays_with_missing_roles()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await db.Create(ct);
        await using var context = db.Context();
        await context.GetService<IMigrator>().MigrateAsync("20261116000000_AzurePriceRefresh", ct);
        await db.Execute("""
            CREATE USER [old operator] WITHOUT LOGIN;
            ALTER ROLE docai_operator ADD MEMBER [old operator];
            CREATE ROLE [owned role] AUTHORIZATION docai_operator;
            CREATE TABLE dbo.RoleOwned (Id int);
            ALTER AUTHORIZATION ON OBJECT::dbo.RoleOwned TO docai_runtime;
            DROP ROLE docai_modelteam;
            """, ct);
        await db.Execute("CREATE SCHEMA [owned schema] AUTHORIZATION docai_operator;", ct);
        await context.Database.MigrateAsync(ct);
        Assert.That(await db.ScalarTarget(Roles, ct), Is.EqualTo("0"));
        Assert.That(await db.ScalarTarget("SELECT USER_NAME(principal_id) FROM sys.schemas WHERE name='owned schema'", ct), Is.EqualTo("dbo"));
        Assert.That(await db.ScalarTarget("SELECT USER_NAME(principal_id) FROM sys.objects WHERE name='RoleOwned'", ct), Is.EqualTo("dbo"));
        Assert.That(await db.ScalarTarget("SELECT USER_NAME(owning_principal_id) FROM sys.database_principals WHERE name='owned role'", ct), Is.EqualTo("dbo"));
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM sys.database_principals WHERE name='old operator'", ct), Is.EqualTo("1"));
        var migration = new RemoveDocAiRoles();
        foreach (var sql in migration.UpOperations.Cast<SqlOperation>()) await db.Execute(sql.Sql, ct);
        foreach (var sql in migration.DownOperations.Cast<SqlOperation>()) await db.Execute(sql.Sql, ct);
        foreach (var sql in migration.DownOperations.Cast<SqlOperation>()) await db.Execute(sql.Sql, ct);
        Assert.That(await db.ScalarTarget(Roles, ct), Is.EqualTo("4"));
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM sys.database_role_members r JOIN sys.database_principals p ON p.principal_id=r.role_principal_id WHERE p.name IN ('docai_runtime','docai_operator','docai_quota_operator','docai_modelteam')", ct), Is.EqualTo("0"));
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM sys.database_permissions r JOIN sys.database_principals p ON p.principal_id=r.grantee_principal_id WHERE p.name IN ('docai_runtime','docai_operator','docai_quota_operator','docai_modelteam')", ct), Is.EqualTo("0"));
        foreach (var sql in migration.UpOperations.Cast<SqlOperation>()) await db.Execute(sql.Sql, ct);
        Assert.That(await db.ScalarTarget(Roles, ct), Is.EqualTo("0"));
    }
}
