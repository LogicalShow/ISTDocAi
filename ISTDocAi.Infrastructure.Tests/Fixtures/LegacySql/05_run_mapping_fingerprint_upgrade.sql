-- Existing DocAi database only; rerunnable under the deployment identity.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID('dbo.JobRuns', 'U') IS NULL
        THROW 50001, 'An existing DocAi JobRuns schema is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.JobRuns')
                   AND name='Reason' AND system_type_id=167 AND max_length IN (10,12) AND is_nullable=0)
        THROW 50001, 'Incompatible JobRuns.Reason metadata.', 1;
    IF COL_LENGTH('dbo.JobRuns', 'MappingFingerprint') IS NULL
        ALTER TABLE dbo.JobRuns ADD MappingFingerprint char(64) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.JobRuns')
                   AND name='MappingFingerprint' AND system_type_id=175 AND max_length=64 AND is_nullable=1)
        THROW 50001, 'Incompatible JobRuns.MappingFingerprint metadata.', 1;
    IF COL_LENGTH('dbo.JobRuns', 'ChangesJson') IS NULL
        ALTER TABLE dbo.JobRuns ADD ChangesJson nvarchar(max) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.JobRuns')
                   AND name='ChangesJson' AND system_type_id=231 AND max_length=-1 AND is_nullable=1)
        THROW 50001, 'Incompatible JobRuns.ChangesJson metadata.', 1;

    CREATE TABLE #ExpectedReason (Reason varchar(12),
        CHECK (Reason IN ('New', 'Requeue', 'Reprocess', 'ModelChanged')));
    DECLARE @expected nvarchar(max) = (SELECT definition FROM tempdb.sys.check_constraints
        WHERE parent_object_id=OBJECT_ID('tempdb..#ExpectedReason'));
    DECLARE @current nvarchar(max) = (SELECT definition FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID('dbo.JobRuns') AND name='CK_Runs_Reason');
    IF COL_LENGTH('dbo.JobRuns','Reason') <> 12 OR @current IS NULL OR @current <> @expected
    BEGIN
        IF @current IS NOT NULL ALTER TABLE dbo.JobRuns DROP CONSTRAINT CK_Runs_Reason;
        ALTER TABLE dbo.JobRuns ALTER COLUMN Reason varchar(12) NOT NULL;
        ALTER TABLE dbo.JobRuns WITH CHECK ADD CONSTRAINT CK_Runs_Reason
            CHECK (Reason IN ('New', 'Requeue', 'Reprocess', 'ModelChanged'));
    END;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.JobRuns')
               AND name='CK_Runs_Reason' AND (is_disabled=1 OR is_not_trusted=1))
        ALTER TABLE dbo.JobRuns WITH CHECK CHECK CONSTRAINT CK_Runs_Reason;
    DROP TABLE #ExpectedReason;
    -- Existing NULL fingerprints intentionally cause exactly one automatic reread after deployment.
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
