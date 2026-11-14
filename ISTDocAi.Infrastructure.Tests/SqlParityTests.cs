using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Tests.DocAi;


public sealed class SqlParityTests
{
    [Test, Category("Sql")]
    public async Task FreshMigrationAndRepeatedIdempotentScriptMatchIndependentOracle()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var ct = timeout.Token;
        await using var oracle = new DisposableDatabase();
        await using var migrated = new DisposableDatabase();
        await using var scripted = new DisposableDatabase();
        await oracle.Create(ct);
        foreach (var file in new[] { "OracleSchema.sql", "OracleRoles.sql" })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, file), ct);
            sql = Regex.Replace(sql, @"^ALTER ROLE[^\r\n]*ADD MEMBER[^\r\n]*", "",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (var batch in SqlBatchReader.Split(sql)) await oracle.Execute(batch, ct);
        }
        Assert.That(await migrated.ScalarMaster($"SELECT COUNT(*) FROM sys.databases WHERE name = N'{migrated.Name}'", ct), Is.EqualTo("0"));
        await using (var context = migrated.Context()) await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", ct);
        var expected = await oracle.Snapshot(ct);
        Assert.That(await migrated.Snapshot(ct), Is.EqualTo(expected));
        await migrated.VerifyHistoryAndRcsi(ct, baselineOnly: true);
        var before = await migrated.Snapshot(ct);
        await using (var context = migrated.Context()) await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", ct);
        Assert.That(await migrated.Snapshot(ct), Is.EqualTo(before));
        await migrated.VerifyHistoryAndRcsi(ct, baselineOnly: true);
        await scripted.Create(ct, enableRcsi: false);
        var script = BaselineTests.Script();
        for (var run = 0; run < 2; run++)
        {
            // One session for all batches preserves EF's explicit transactions and SET options.
            await scripted.ExecuteBatches(SqlBatchReader.Split(script), ct);
            Assert.That(await scripted.Snapshot(ct), Is.EqualTo(expected));
            await scripted.VerifyHistoryAndRcsi(ct, baselineOnly: true);
        }
    }

}
