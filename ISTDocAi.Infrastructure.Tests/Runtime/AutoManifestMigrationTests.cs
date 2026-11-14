using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class AutoManifestMigrationTests
{
    private static Task Upgrade(SqlDatabase db) => Apply(db, "03_auto_manifest_upgrade.sql");
    private static async Task Apply(SqlDatabase db, string file) => await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, file)));

    [Test, Category("Sql")]
    public async Task Upgrade_twice_backfills_old_rows_preserves_history_and_rejects_invalid_routes()
    {
        SqlTestGuard.Require();
        var db = new SqlDatabase { UseLegacySchema = true };
        try
        {
            await db.InitializeAsync();
            var id = Guid.NewGuid();
            await db.Execute("""
                INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version) VALUES('bl-classifier','BL','classify',1);
                INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,ShippingLineCode) VALUES('bl-classifier','maersk','MAERSK');
                INSERT dbo.Jobs(JobId,ConsumerId,DocumentTypeCode,RequestedShippingLineCode,IdemLine,ShippingLineCode,
                    FileSha256,ContentType,FileSizeBytes,PageCount,BlobPath,Stage,Status,RunNo,CorrelationId)
                VALUES(@id,1,'BL','LINE','LINE','LINE',CONVERT(binary(32),1),'application/pdf',100,2,'file','Map','Completed',3,'test');
                INSERT dbo.JobRuns(JobId,RunNo,Reason,ConsumerId,PageCount,CorrelationId,Status) VALUES
                    (@id,1,'New',1,2,'old','Completed'),(@id,2,'Reprocess',1,2,'old2','Completed'),(@id,3,'Reprocess',1,2,'current','Completed');
                INSERT dbo.JobResults(JobId,RunNo,ContractVersion,ResultJson,FlaggedFields) VALUES
                    (@id,1,'1.0',N'{"documentTypeCode":"BL","fields":{}}',0),
                    (@id,2,'1.0',N'{"documentTypeCode":"INV","fields":{}}',0);
                """, new { id });
            var originalResults = await db.Scalar<string>("SELECT * FROM dbo.JobResults ORDER BY RunNo FOR JSON PATH");
            await Upgrade(db);
            Assert.Equal("BL", await db.Scalar<string>("SELECT ResolvedDocumentTypeCode FROM dbo.Jobs"));
            Assert.Equal("INV", await db.Scalar<string>("SELECT TargetDocumentTypeCode FROM dbo.ClassifierClasses WHERE ClassifierModelId='inv-classifier'"));
            Assert.Equal("BL", await db.Scalar<string>("SELECT TargetDocumentTypeCode FROM dbo.ClassifierClasses WHERE ClassifierModelId='bl-classifier'"));
            Assert.Equal(2, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE ResolvedDocumentTypeCode='BL'"));
            Assert.Equal("INV", await db.Scalar<string>("SELECT ResolvedDocumentTypeCode FROM dbo.JobRuns WHERE RunNo=2"));
            Assert.Equal("bill-of-lading.v1", await db.Scalar<string>("SELECT ResultType FROM dbo.JobRuns WHERE RunNo=1"));
            Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE ResultType IS NULL"));
            Assert.Equal(originalResults, await db.Scalar<string>("SELECT * FROM dbo.JobResults ORDER BY RunNo FOR JSON PATH"));
            await db.Execute("""
                INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version) VALUES('router','AUTO','classify',1);
                INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode)
                VALUES('router','other',NULL,NULL);
                """);
            var before = await db.Snapshot();
            var classes = await db.Scalar<string>("SELECT * FROM dbo.ClassifierClasses ORDER BY ClassifierModelId,ClassName FOR JSON PATH,INCLUDE_NULL_VALUES");
            await Upgrade(db);
            Assert.Equal(before, await db.Snapshot());
            Assert.Equal(classes, await db.Scalar<string>("SELECT * FROM dbo.ClassifierClasses ORDER BY ClassifierModelId,ClassName FOR JSON PATH,INCLUDE_NULL_VALUES"));
            foreach (var sql in new[]
            {
                "UPDATE dbo.Jobs SET ResolvedDocumentTypeCode='AUTO'",
                "UPDATE dbo.JobRuns SET ResolvedDocumentTypeCode='AUTO'",
                "UPDATE dbo.ClassifierClasses SET TargetDocumentTypeCode='AUTO' WHERE ClassifierModelId='router'",
                "UPDATE dbo.ClassifierClasses SET ShippingLineCode='LINE' WHERE ClassifierModelId='router'"
            })
                Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Execute(sql))).Number);
        }
        finally { await db.DisposeAsync(); }
    }

    [Test, Category("Sql")]
    public async Task Fresh_and_upgraded_schemas_match_columns_checks_and_indexes()
    {
        SqlTestGuard.Require();
        var fresh = new SqlDatabase { UseSchemaScript = true }; var legacy = new SqlDatabase { UseLegacySchema = true };
        try
        {
            await fresh.InitializeAsync(); await legacy.InitializeAsync(); await Upgrade(legacy);
            await Apply(legacy, "04_consumer_page_limit_upgrade.sql");
            await Apply(legacy, "05_run_mapping_fingerprint_upgrade.sql");
            await Apply(legacy, "06_page_quota_upgrade.sql");
            await Apply(legacy, "07_topup_reporting_upgrade.sql");
            Assert.Equal(await Metadata(fresh), await Metadata(legacy));
            var before = await fresh.Snapshot();
            await Upgrade(fresh);
            await Apply(fresh, "04_consumer_page_limit_upgrade.sql");
            await Apply(fresh, "05_run_mapping_fingerprint_upgrade.sql");
            await Apply(fresh, "06_page_quota_upgrade.sql");
            await Apply(fresh, "07_topup_reporting_upgrade.sql");
            Assert.Equal(before, await fresh.Snapshot());
            Assert.Equal(await Metadata(fresh), await Metadata(legacy));
        }
        finally { await fresh.DisposeAsync(); await legacy.DisposeAsync(); }
    }

    [Test, Category("Sql")]
    public async Task Consumer_page_upgrade_is_idempotent_and_enforces_boundaries()
    {
        SqlTestGuard.Require();
        var db = new SqlDatabase { UseLegacySchema = true };
        try
        {
            await db.InitializeAsync();
            await Apply(db, "04_consumer_page_limit_upgrade.sql");
            var before = await Metadata(db);
            var constraintId = await db.Scalar<int>("SELECT OBJECT_ID('dbo.CK_Consumers_Limits')");
            await Apply(db, "04_consumer_page_limit_upgrade.sql");
            Assert.Equal(before, await Metadata(db));
            Assert.Equal(constraintId, await db.Scalar<int>("SELECT OBJECT_ID('dbo.CK_Consumers_Limits')"));
            Assert.Equal(10, await db.Scalar<int>("SELECT MaxPagesPerJob FROM dbo.Consumers WHERE ConsumerId=1"));
            foreach (var pages in new[] { 200, 2000 })
                await db.Execute("UPDATE dbo.Consumers SET MaxPagesPerJob=@pages WHERE ConsumerId=1", new { pages });
            foreach (var sql in new[] { "UPDATE dbo.Consumers SET MaxPagesPerJob=2001", "UPDATE dbo.Consumers SET MaxPagesPerJob=0", "UPDATE dbo.Consumers SET DailyPageCap=0" })
                Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Execute(sql))).Number);
        }
        finally { await db.DisposeAsync(); }
    }

    [Test, Category("Sql")]
    public async Task Incompatible_metadata_rolls_back_the_entire_upgrade()
    {
        SqlTestGuard.Require();
        var db = new SqlDatabase { UseLegacySchema = true };
        try
        {
            await db.InitializeAsync();
            await db.Execute("ALTER TABLE dbo.Jobs ADD ResolvedDocumentTypeCode nvarchar(20) NULL;");
            var before = await db.Snapshot();
            Assert.Equal(50001, (await Assert.ThrowsAsync<SqlException>(() => Upgrade(db))).Number);
            Assert.Equal(before, await db.Snapshot());
            Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.JobRuns') AND name='ResultType'"));
            Assert.Equal(0, await db.Scalar<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClassifierClasses') AND name='TargetDocumentTypeCode'"));
            Assert.Equal(0, await db.Scalar<int>("SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClassifierClasses') AND name='ShippingLineCode'"));
        }
        finally { await db.DisposeAsync(); }
    }

    private static Task<string> Metadata(SqlDatabase db) => db.Scalar<string>("""
        SELECT
        (SELECT OBJECT_NAME(c.object_id) AS tableName,c.name,t.name AS typeName,c.max_length,c.precision,c.scale,
                c.is_nullable,c.is_identity,c.is_computed,dc.name AS defaultName,dc.definition AS defaultDefinition
         FROM sys.columns AS c JOIN sys.tables AS tb ON tb.object_id=c.object_id
         JOIN sys.types AS t ON t.user_type_id=c.user_type_id
         LEFT JOIN sys.default_constraints AS dc ON dc.object_id=c.default_object_id
         WHERE SCHEMA_NAME(tb.schema_id)='dbo' ORDER BY tableName,c.name FOR JSON PATH,INCLUDE_NULL_VALUES) AS columns,
        (SELECT OBJECT_NAME(parent_object_id) AS tableName,name,definition,is_disabled,is_not_trusted
         FROM sys.check_constraints WHERE SCHEMA_NAME(schema_id)='dbo' ORDER BY tableName,name FOR JSON PATH) AS checks,
        (SELECT OBJECT_NAME(i.object_id) AS tableName,i.name,i.type_desc,i.is_unique,i.is_primary_key,i.is_unique_constraint,
                i.filter_definition,i.is_disabled,
                (SELECT c.name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column
                 FROM sys.index_columns AS ic JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
                 ORDER BY ic.index_column_id FOR JSON PATH) AS columns
         FROM sys.indexes AS i JOIN sys.tables AS tb ON tb.object_id=i.object_id
         WHERE SCHEMA_NAME(tb.schema_id)='dbo' AND i.name IS NOT NULL ORDER BY tableName,i.name FOR JSON PATH,INCLUDE_NULL_VALUES) AS indexes
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER
        """);
}
