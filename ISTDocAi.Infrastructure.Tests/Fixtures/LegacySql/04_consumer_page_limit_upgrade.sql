-- Existing DocAi database only; run under the deployment identity.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID('dbo.Consumers', 'U') IS NULL
       OR COL_LENGTH('dbo.Consumers', 'MaxPagesPerJob') IS NULL
       OR COL_LENGTH('dbo.Consumers', 'DailyPageCap') IS NULL
        THROW 50001, 'An existing DocAi Consumers schema is required.', 1;

    -- Compare SQL Server's parsed definition, rather than handwritten parentheses/whitespace.
    CREATE TABLE #ExpectedConsumerLimits (MaxPagesPerJob smallint, DailyPageCap int,
        CHECK (MaxPagesPerJob BETWEEN 1 AND 2000 AND DailyPageCap > 0));
    DECLARE @expected nvarchar(max) = (SELECT definition FROM tempdb.sys.check_constraints
        WHERE parent_object_id = OBJECT_ID('tempdb..#ExpectedConsumerLimits'));
    DECLARE @current nvarchar(max) = (SELECT definition FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID('dbo.Consumers') AND name = 'CK_Consumers_Limits');
    IF @current IS NULL OR @current <> @expected
    BEGIN
        IF @current IS NOT NULL
            ALTER TABLE dbo.Consumers DROP CONSTRAINT CK_Consumers_Limits;
        ALTER TABLE dbo.Consumers WITH CHECK ADD CONSTRAINT CK_Consumers_Limits
            CHECK (MaxPagesPerJob BETWEEN 1 AND 2000 AND DailyPageCap > 0);
    END;
    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID('dbo.Consumers') AND name = 'CK_Consumers_Limits'
          AND (is_disabled = 1 OR is_not_trusted = 1))
        ALTER TABLE dbo.Consumers WITH CHECK CHECK CONSTRAINT CK_Consumers_Limits;
    DROP TABLE #ExpectedConsumerLimits;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
