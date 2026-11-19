using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed partial class MappingProfilesTests
{
    [Test]
    public void New_migration_is_idempotently_scripted_offline_with_wrapped_modules()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.That(script, Does.Contain("20261103000000_ModelMappingProfiles"));
        foreach (var batch in SqlBatchReader.Split(SqlBatchReader.ReadResource("ModelMappingProfiles.sql")))
            if (batch.Contains("CREATE TRIGGER", StringComparison.Ordinal))
                Assert.That(System.Text.RegularExpressions.Regex.Replace(script, @"\s+", " "), Does.Contain(System.Text.RegularExpressions.Regex.Replace(SqlBatchReader.PrepareBatch(batch), @"\s+", " ")));
        Assert.Throws<NotSupportedException>(() => context.GetService<IMigrator>().GenerateScript(
            "20261103000000_ModelMappingProfiles", "20261101000000_InitialSchema"));
    }

    [Test]
    public void Mapping_migration_only_creates_triggers_on_new_mapping_tables()
    {
        var sql = SqlBatchReader.ReadResource("ModelMappingProfiles.sql");
        var triggers = System.Text.RegularExpressions.Regex.Matches(sql, @"CREATE TRIGGER dbo\.(\w+) ON dbo\.(\w+)");
        Assert.That(triggers.Count, Is.EqualTo(3));
        Assert.That(triggers.Select(t => t.Groups[2].Value), Is.EqualTo(new[] { "MappingProfiles", "MappingFields", "ModelMappingBindings" }));
        Assert.That(sql, Does.Not.Contain("UPDATE dbo.ModelRegistry"));
        Assert.That(sql, Does.Not.Contain("UPDATE m SET ActivatedAtUtc"));
    }

    [Test, Category("Sql")]
    public async Task Latest_migration_preserves_triggers_on_all_initial_schema_tables()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Baseline(db, ct);
        var tables = System.Text.RegularExpressions.Regex.Matches(SqlBatchReader.ReadResource("InitialSchema.sql"),
            @"CREATE TABLE dbo\.(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.That(tables, Is.Not.Empty);

        async Task<(string Table, string Trigger, string Definition)[]> CaptureTriggers()
        {
            await using var connection = new SqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("""
                SELECT t.name, tr.name, OBJECT_DEFINITION(tr.object_id)
                FROM sys.triggers tr
                JOIN sys.tables t ON t.object_id = tr.parent_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = 'dbo'
                ORDER BY t.name, tr.name
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var triggers = new List<(string Table, string Trigger, string Definition)>();
            while (await reader.ReadAsync(ct))
            {
                if (!tables.Contains(reader.GetString(0))) continue;
                Assert.That(reader.IsDBNull(2), Is.False ,"Trigger definition must be visible for comparison.");
                triggers.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
            return triggers.ToArray();
        }

        var baselineTriggers = await CaptureTriggers();
        await Latest(db, ct);
        Assert.That(await CaptureTriggers(), Is.EqualTo(baselineTriggers));
        // An unbound, never-used draft still supports all field mutations.
        await db.Execute("""
            UPDATE dbo.MappingProfiles SET ChangeReference='draft edit' WHERE ProfileCode='legacy';
            INSERT dbo.MappingFields SELECT ProfileId,'Field','extra','Extra' FROM dbo.MappingProfiles WHERE ProfileCode='legacy';
            UPDATE dbo.MappingFields SET AzureName='Edited' WHERE CanonicalKey='extra';
            DELETE dbo.MappingFields WHERE CanonicalKey='extra';
            """, ct);
    }

    private static void AssertFieldsSealed(DisposableDatabase db, string code, CancellationToken ct)
    {
        foreach (var mutation in new[] {
            "UPDATE dbo.MappingFields SET AzureName='Tamper' WHERE ProfileId=",
            "DELETE dbo.MappingFields WHERE ProfileId=",
            "INSERT dbo.MappingFields SELECT ProfileId,'Field','extra','Extra' FROM dbo.MappingProfiles WHERE ProfileId=" })
        {
            var error = Assert.ThrowsAsync<SqlException>(() => db.Execute(mutation +
                $"(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='{code}')", ct));
            Assert.That(error.Number, Is.EqualTo(51200));
        }
    }

    private static async Task Baseline(DisposableDatabase db, CancellationToken ct)
    {
        await using var context = db.Context();
        await context.GetService<IMigrator>().MigrateAsync("20261101000000_InitialSchema", ct);
    }
    private static async Task Latest(DisposableDatabase db, CancellationToken ct)
    {
        await using var context = db.Context();
        await context.Database.MigrateAsync(ct);
    }





    [Test, Category("Sql")]
    public async Task Recorded_operation_seals_a_never_active_model_and_late_binding()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        foreach (var lateBinding in new[] { false, true })
        {
            await using var db = new DisposableDatabase();
            await Latest(db, ct);
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('used-draft','extract','BL',1,'Draft')", ct);
            const string binding = "INSERT dbo.ModelMappingBindings SELECT 'used-draft',ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3'";
            if (!lateBinding) await db.Execute(binding, ct);
            await db.Execute("""
                INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1)
                  VALUES ('MAP',N'Mapping test','FWD','MAP',HASHBYTES('SHA2_256','fixture'));
                DECLARE @consumer smallint=SCOPE_IDENTITY(), @job uniqueidentifier=NEWID();
                INSERT dbo.Jobs(JobId,ConsumerId,DocumentTypeCode,IdemLine,FileSha256,ContentType,FileSizeBytes,PageCount,BlobPath,Stage,CorrelationId,Status)
                  VALUES (@job,@consumer,'BL','*',HASHBYTES('SHA2_256','document'),'application/pdf',100,1,'fixture','Extract','mapping','Completed');
                INSERT dbo.JobRuns(JobId,RunNo,Reason,ConsumerId,PageCount,CorrelationId,Status)
                  VALUES (@job,1,'New',@consumer,1,'mapping','Completed');
                INSERT dbo.AzureOperations(JobId,RunNo,Kind,ModelId,State,OperationLocation,Pages)
                  VALUES (@job,1,'extract','used-draft','Succeeded','fixture',1);
                """, ct);
            if (lateBinding)
            {
                var error = Assert.ThrowsAsync<SqlException>(() => db.Execute(binding, ct));
                Assert.That(error.Number, Is.EqualTo(51200));
                continue;
            }
            AssertFieldsSealed(db, "bl-v3", ct);
            Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3' AND SealedAtUtc IS NOT NULL", ct), Is.EqualTo("0"));
            Assert.ThrowsAsync<SqlException>(() => db.Execute("UPDATE dbo.MappingProfiles SET ChangeReference='tamper' WHERE ProfileCode='bl-v3'", ct));
        }
    }

    [Test, Category("Sql")]
    public async Task Limits_and_invalid_keys_are_enforced_and_failed_writes_are_atomic()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Latest(db, ct);
        foreach (var statement in new[] {
            "UPDATE dbo.MappingProfiles SET ProfileVersion=0 WHERE ProfileCode='legacy'",
            "UPDATE dbo.MappingProfiles SET ContainersField='bad name' WHERE ProfileCode='legacy'",
            "INSERT dbo.MappingFields SELECT ProfileId,'Field','1bad','Good' FROM dbo.MappingProfiles WHERE ProfileCode='legacy'",
            "INSERT dbo.MappingFields SELECT ProfileId,'Field','good','bad name' FROM dbo.MappingProfiles WHERE ProfileCode='legacy'",
            "INSERT dbo.MappingFields SELECT ProfileId,'Header','good','Good' FROM dbo.MappingProfiles WHERE ProfileCode='legacy'",
            "WITH n AS (SELECT TOP(257) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) n FROM sys.all_objects) INSERT dbo.MappingFields SELECT p.ProfileId,'Field',CONCAT('extra',n.n),'Extra' FROM n CROSS JOIN dbo.MappingProfiles p WHERE p.ProfileCode='legacy'" })
            Assert.ThrowsAsync<SqlException>(() => db.Execute(statement, ct));
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingFields WHERE CanonicalKey LIKE 'extra%'", ct), Is.EqualTo("0"));
    }

    [Test, Category("Sql")]
    public async Task Mapping_edits_after_committed_activation_are_refused()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Latest(db, ct);
        await db.Execute("INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('racing','extract','BL',1,'Draft'); INSERT dbo.ModelMappingBindings SELECT 'racing',ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3';", ct);
        async Task Attempt(string sql)
        {
            try { await db.Execute(sql, ct); }
            catch (SqlException error) when (error.Number is 1205 or 51200) { }
        }
        await Task.WhenAll(
            Attempt("UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId='racing'"),
            Attempt("UPDATE dbo.MappingFields SET AzureName='Raced' WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3')"));
        // Finish activation before checking subsequent edits; runtime activation does not lock mapping profiles.
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId='racing'", ct);
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3' AND SealedAtUtc IS NOT NULL", ct), Is.EqualTo("0"));
        Assert.ThrowsAsync<SqlException>(() => db.Execute("UPDATE dbo.MappingFields SET AzureName='AfterSeal' WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='bl-v3')", ct));
    }

    [Test, Category("Sql")]
    public async Task Draft_edits_activation_and_retirement_preserve_sealing()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Latest(db, ct);
        await db.Execute("""
            INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('new-draft','extract','BL',1,'Draft');
            INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference)
             VALUES ('new-draft',1,'BL','ModelProfile','Containers','containerNumber','sealNumber','BillsOfLading','test');
            INSERT dbo.MappingFields SELECT ProfileId,'Field','sample','Sample' FROM dbo.MappingProfiles WHERE ProfileCode='new-draft';
            INSERT dbo.ModelMappingBindings SELECT 'new-draft',ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='new-draft';
            UPDATE dbo.MappingFields SET AzureName='Edited' WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='new-draft');
            UPDATE dbo.MappingProfiles SET ChangeReference='edited' WHERE ProfileCode='new-draft';
            """, ct);
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Testing',TestFieldAccuracy=1 WHERE ModelId='new-draft'", ct);
        await db.Execute("DECLARE @newModelId varchar(64)='new-draft', @minimumAccuracy decimal(5,4)=0.95;\n" + new SqlStatementCatalog()["S14"], ct);
        AssertFieldsSealed(db, "new-draft", ct);
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='new-draft'", ct);
        Assert.That(await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingProfiles WHERE ProfileCode='new-draft' AND SealedAtUtc IS NOT NULL", ct), Is.EqualTo("0"));
        foreach (var statement in new[] {
            "UPDATE dbo.MappingProfiles SET ChangeReference='tamper' WHERE ProfileCode='new-draft'",
            "DELETE dbo.MappingProfiles WHERE ProfileCode='new-draft'",
            "UPDATE dbo.MappingFields SET AzureName='Tamper' WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='new-draft')",
            "DELETE dbo.MappingFields WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='new-draft')",
            "INSERT dbo.MappingFields SELECT ProfileId,'Field','other','Other' FROM dbo.MappingProfiles WHERE ProfileCode='new-draft'",
            "UPDATE dbo.ModelMappingBindings SET ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='legacy') WHERE ModelId='new-draft'",
            "DELETE dbo.ModelMappingBindings WHERE ModelId='new-draft'" })
            Assert.ThrowsAsync<SqlException>(() => db.Execute(statement, ct));
    }
}
