using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class BaselineTests
{
        [TestCase("", 0)]
    [TestCase("  \r\nGO\r\n", 0)]
    [TestCase("SELECT 1;\r\n go \r\nSELECT 2;", 2)]
    [TestCase("SELECT 'GO';\nGO -- comment\nGOTO label;", 1)]
    [TestCase("SELECT 1;\nGO 2\nSELECT 2;", 1)]
    public void StandaloneGoOnly(string sql, int count) => Assert.That(SqlBatchReader.Split(sql).Count(), Is.EqualTo(count));

    [Test]
    public void NullIsRejected() => Assert.Throws<ArgumentNullException>(() => SqlBatchReader.Split(null!));

        [TestCase("InitialRoles.sql", "01AD572F9B438128E6AE1E1E8F2EDDF134B053F148D3689EDCB75755AD2103A9")]
    [TestCase("InitialSchema.sql", "0637C3BC40F092F3E70002C570EA29B5961C7F9849916D3F84009FAAEA6142CD")]
    public void FrozenResources(string name, string hash)
    {
        using var stream = typeof(DocAiDbContext).Assembly.GetManifestResourceStream("DocAi.Database.Migrations.Sql." + name);
        Assert.That(stream, Is.Not.Null);
        Assert.That(Convert.ToHexString(SHA256.HashData(stream!)), Is.EqualTo(hash));
    }

    [Test]
    public void MembershipsAreExcluded()
    {
        var sql = SqlBatchReader.ReadResource("InitialRoles.sql");
        Assert.That(sql, Does.Not.Contain("ADD MEMBER").IgnoreCase);
        Assert.That(sql, Does.Not.Contain("$"));
        Assert.That(sql, Does.Contain("DENY"));
    }

    [Test]
    public void IdempotentScriptIsGeneratedWithoutConnection()
    {
        var script = Script();
        Assert.That(script, Does.Contain("20261101000000_InitialSchema"));
        Assert.That(script, Does.Contain("10.0.13"));
        Assert.That(script, Does.Not.Contain("ROLLBACK IMMEDIATE"));
        foreach (var file in new[] { "InitialSchema.sql", "InitialRoles.sql" })
            foreach (var batch in SqlBatchReader.Split(SqlBatchReader.ReadResource(file)))
            {
                var prepared = SqlBatchReader.PrepareBatch(batch);
                Assert.That(Regex.IsMatch(prepared, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase), Is.False);
                if (prepared.StartsWith("EXEC(N'", StringComparison.Ordinal))
                    Assert.That(Regex.Replace(script, @"\s+", " "), Does.Contain(Regex.Replace(prepared, @"\s+", " ")));
            }
    }

    [Test]
    public void ModulesEscapeQuotesAndRollbackIsUnsupported()
    {
        Assert.That(SqlBatchReader.PrepareBatch("CREATE VIEW dbo.V AS SELECT 'x' AS X;"), Is.EqualTo("EXEC(N'CREATE VIEW dbo.V AS SELECT ''x'' AS X;');"));
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        Assert.Throws<NotSupportedException>(() => context.GetService<IMigrator>().GenerateScript("20261101000000_InitialSchema", "0"));
        var options = new DatabaseOptions();
        Assert.That(options.AllowDatabaseCreation, Is.True);
        Assert.That(options.MigrateOnStartup, Is.True);
        Assert.That(options.MigrationConnectionString, Is.Null);
        Assert.That(options.CommandTimeoutSeconds, Is.EqualTo(300));
        Assert.That(options.InitializationTimeoutSeconds, Is.EqualTo(900));
    }

    [Test]
    public void CachedSqlServerProviderEnablesRcsiDuringCreationOutsideTransaction()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(
            [new Microsoft.EntityFrameworkCore.Migrations.Operations.SqlServerCreateDatabaseOperation { Name = "OfflineProviderProbe" }]);
        Assert.That(commands.Any(command => command.CommandText.Contains("READ_COMMITTED_SNAPSHOT ON", StringComparison.Ordinal)
            && command.TransactionSuppressed), Is.True);
        Assert.That(commands.Any(command => command.CommandText.Contains("CREATE DATABASE", StringComparison.Ordinal)
            && command.TransactionSuppressed), Is.True);
    }

    internal static string Script()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        return context.GetService<IMigrator>().GenerateScript(toMigration: "20261101000000_InitialSchema", options: MigrationsSqlGenerationOptions.Idempotent);
    }
}
