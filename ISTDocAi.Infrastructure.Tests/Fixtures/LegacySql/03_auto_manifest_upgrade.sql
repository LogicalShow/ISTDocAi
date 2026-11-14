-- added after plan rev 2.9; plan update pending
-- Existing DocAi database only. Stop API/workers first; run under the deployment identity.
-- Forward-only expansion: retain the new columns/data on application rollback. Do not replay bootstrap.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID('dbo.Jobs', 'U') IS NULL OR OBJECT_ID('dbo.JobRuns', 'U') IS NULL
       OR OBJECT_ID('dbo.ClassifierClasses', 'U') IS NULL
        THROW 50001, 'An existing DocAi schema is required.', 1;

    DECLARE @jobsAdded bit = 0, @runsAdded bit = 0, @resultAdded bit = 0, @classesAdded bit = 0;
    -- [M1]
    IF COL_LENGTH('dbo.Jobs', 'ResolvedDocumentTypeCode') IS NULL
    BEGIN
        ALTER TABLE dbo.Jobs ADD ResolvedDocumentTypeCode varchar(20) NULL;
        SET @jobsAdded = 1;
    END;
    -- [M2]
    IF COL_LENGTH('dbo.JobRuns', 'ResolvedDocumentTypeCode') IS NULL
    BEGIN
        ALTER TABLE dbo.JobRuns ADD ResolvedDocumentTypeCode varchar(20) NULL;
        SET @runsAdded = 1;
    END;
    IF COL_LENGTH('dbo.JobRuns', 'ResultType') IS NULL
    BEGIN
        ALTER TABLE dbo.JobRuns ADD ResultType varchar(32) NULL;
        SET @resultAdded = 1;
    END;
    -- [M3]
    IF COL_LENGTH('dbo.ClassifierClasses', 'TargetDocumentTypeCode') IS NULL
    BEGIN
        ALTER TABLE dbo.ClassifierClasses ADD TargetDocumentTypeCode varchar(20) NULL;
        SET @classesAdded = 1;
    END;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ClassifierClasses')
               AND name = 'ShippingLineCode' AND is_nullable = 0)
        ALTER TABLE dbo.ClassifierClasses ALTER COLUMN ShippingLineCode varchar(20) NULL;

    -- [M4] Compile dependent statements only after their columns exist. Backfill once, never on rerun:
    -- later null targets (intentional rejection) and unresolved AUTO runs must stay null.
    IF @classesAdded = 1
        EXEC sys.sp_executesql N'UPDATE c SET TargetDocumentTypeCode = m.DocumentTypeCode
            FROM dbo.ClassifierClasses AS c JOIN dbo.ModelRegistry AS m ON m.ModelId = c.ClassifierModelId
            WHERE m.DocumentTypeCode <> ''AUTO'' AND c.TargetDocumentTypeCode IS NULL;';
    IF @jobsAdded = 1
        EXEC sys.sp_executesql N'UPDATE dbo.Jobs SET ResolvedDocumentTypeCode = DocumentTypeCode
            WHERE DocumentTypeCode <> ''AUTO'' AND ResolvedDocumentTypeCode IS NULL;';

    -- [M5] History comes from each run''s own result, never the current job''s resolved route.
    IF @runsAdded = 1
        EXEC sys.sp_executesql N'UPDATE r SET ResolvedDocumentTypeCode =
            COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(res.ResultJson) = 1 THEN res.ResultJson ELSE N''{}'' END,
                                      ''$.documentTypeCode''), ''AUTO''), NULLIF(j.DocumentTypeCode, ''AUTO''))
            FROM dbo.JobRuns AS r JOIN dbo.Jobs AS j ON j.JobId = r.JobId
            LEFT JOIN dbo.JobResults AS res ON res.JobId = r.JobId AND res.RunNo = r.RunNo
            WHERE r.ResolvedDocumentTypeCode IS NULL
              AND (j.DocumentTypeCode <> ''AUTO'' OR JSON_VALUE(CASE WHEN ISJSON(res.ResultJson) = 1 THEN res.ResultJson ELSE N''{}'' END,
                                                              ''$.documentTypeCode'') IS NOT NULL);';
    IF @resultAdded = 1
        EXEC sys.sp_executesql N'UPDATE r SET ResultType = ''bill-of-lading.v1''
            FROM dbo.JobRuns AS r
            WHERE r.Status = ''Completed'' AND r.ResolvedDocumentTypeCode = ''BL'' AND r.ResultType IS NULL;';

    -- [M6] Use the same names and definitions as the fresh-install schema.
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Jobs') AND name = 'CK_Jobs_ResolvedType')
        EXEC(N'ALTER TABLE dbo.Jobs WITH CHECK ADD CONSTRAINT CK_Jobs_ResolvedType CHECK (ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> ''AUTO'');');
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.JobRuns') AND name = 'CK_Runs_ResolvedType')
        EXEC(N'ALTER TABLE dbo.JobRuns WITH CHECK ADD CONSTRAINT CK_Runs_ResolvedType CHECK (ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> ''AUTO'');');
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.ClassifierClasses') AND name = 'CK_Class_Target')
        EXEC(N'ALTER TABLE dbo.ClassifierClasses WITH CHECK ADD CONSTRAINT CK_Class_Target CHECK (TargetDocumentTypeCode IS NULL OR TargetDocumentTypeCode <> ''AUTO'');');
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.ClassifierClasses') AND name = 'CK_Class_TargetLine')
        EXEC(N'ALTER TABLE dbo.ClassifierClasses WITH CHECK ADD CONSTRAINT CK_Class_TargetLine CHECK (TargetDocumentTypeCode IS NOT NULL OR ShippingLineCode IS NULL);');

    -- [M7] Fail and roll back for incompatible metadata or disabled/untrusted checks. No data repair on rerun.
    IF EXISTS (SELECT 1 FROM sys.columns AS c JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE (c.object_id = OBJECT_ID('dbo.Jobs') AND c.name = 'ResolvedDocumentTypeCode'
            OR c.object_id = OBJECT_ID('dbo.JobRuns') AND c.name IN ('ResolvedDocumentTypeCode','ResultType')
            OR c.object_id = OBJECT_ID('dbo.ClassifierClasses') AND c.name IN ('TargetDocumentTypeCode','ShippingLineCode'))
          AND (t.name <> 'varchar' OR c.is_nullable <> 1 OR c.max_length <> CASE WHEN c.name = 'ResultType' THEN 32 ELSE 20 END))
        THROW 50001, 'AUTO/MANIFEST column metadata is incompatible.', 1;
    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE name IN ('CK_Jobs_ResolvedType','CK_Runs_ResolvedType','CK_Class_Target','CK_Class_TargetLine')
          AND parent_object_id IN (OBJECT_ID('dbo.Jobs'),OBJECT_ID('dbo.JobRuns'),OBJECT_ID('dbo.ClassifierClasses'))
          AND (is_disabled = 1 OR is_not_trusted = 1))
        THROW 50001, 'AUTO/MANIFEST checks must be enabled and trusted.', 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
