-- Stop API/workers and back up first. Additive, transactional, rerunnable; never deletes accounting data.
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.PageAllowanceGrants','U') IS NULL THROW 50001,'Apply 06_page_quota_upgrade.sql first.',1;
-- Dynamic batch preserves the migration batch-compile split pattern.
EXEC(N'
DECLARE @ids nvarchar(max), @message nvarchar(2048);
SELECT @ids=STRING_AGG(CONVERT(nvarchar(max),GrantId),'','') WITHIN GROUP (ORDER BY GrantId)
FROM dbo.PageAllowanceGrants
WHERE Kind=''Reversal'' AND OriginalGrantId IN
 (SELECT OriginalGrantId FROM dbo.PageAllowanceGrants WHERE Kind=''Reversal'' GROUP BY OriginalGrantId HAVING COUNT(*)>1);
IF @ids IS NOT NULL
BEGIN
 SET @message=CONCAT(''Duplicate reversals; inspect GrantIds: '',LEFT(@ids,1950),''. No data changed.'');
 THROW 50019,@message,1;
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.PageAllowanceGrants'') AND name=''UX_Allowance_Reversal'')
 CREATE UNIQUE INDEX UX_Allowance_Reversal ON dbo.PageAllowanceGrants(OriginalGrantId) WHERE Kind=''Reversal'';
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.CustomerPageCharges'') AND name=''IX_PageCharges_Consumer'')
 DROP INDEX IX_PageCharges_Consumer ON dbo.CustomerPageCharges;
CREATE INDEX IX_PageCharges_Consumer ON dbo.CustomerPageCharges(ConsumerId,RecordedAtUtc,ChargeId) INCLUDE(Pages,JobId,RunNo);
');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Allowance_Insert ON dbo.PageAllowanceGrants INSTEAD OF INSERT AS
BEGIN
    SET NOCOUNT ON;
    -- One audit request at a time; the lock precedes every accounting read/write.
    IF (SELECT COUNT(*) FROM inserted)<>1 THROW 50017, ''One allowance request is required.'', 1;
    DECLARE @consumerId smallint=(SELECT ConsumerId FROM inserted);
    EXEC dbo.AcquirePageQuotaLock @consumerId;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.RequestId=i.RequestId)
    BEGIN
        IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.RequestId=i.RequestId
            WHERE g.ConsumerId<>i.ConsumerId OR g.Pages<>i.Pages OR g.Kind<>i.Kind
            OR ISNULL(g.OriginalGrantId,0)<>ISNULL(i.OriginalGrantId,0)
            OR g.OperatorIdentity<>i.OperatorIdentity OR g.Note<>i.Note)
            THROW 50017, ''Allowance request identity conflict.'', 1;
        RETURN;
    END;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.GrantId=i.OriginalGrantId
              WHERE i.Kind=''Reversal'' AND (i.Pages<>-g.Pages OR i.ConsumerId<>g.ConsumerId OR g.Kind<>''TopUp''))
        THROW 50017, ''Invalid allowance reversal.'', 1;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants r ON r.OriginalGrantId=i.OriginalGrantId
              WHERE i.Kind=''Reversal'' AND r.Kind=''Reversal'')
        THROW 50017, ''TopUp already reversed.'', 1;
    INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId,BalanceAfter)
    SELECT i.ConsumerId,i.Pages,i.Kind,i.OriginalGrantId,i.OperatorIdentity,i.Note,i.RequestId,b.AvailablePages+i.Pages
    FROM inserted i JOIN dbo.ConsumerPageBalance b ON b.ConsumerId=i.ConsumerId;
END;');
-- Existing quota-operator SELECT grants already cover these objects; no Jobs or Azure meter access needed.
IF DATABASE_PRINCIPAL_ID('docai_quota_operator') IS NOT NULL
BEGIN
 GRANT SELECT ON dbo.Consumers TO docai_quota_operator;
 GRANT SELECT ON dbo.PageAllowanceGrants TO docai_quota_operator;
 GRANT SELECT ON dbo.CustomerPageCharges TO docai_quota_operator;
 GRANT SELECT ON dbo.PageReservations TO docai_quota_operator;
 GRANT SELECT ON dbo.ConsumerPageBalance TO docai_quota_operator;
 GRANT SELECT ON dbo.JobRuns TO docai_quota_operator;
END;
COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
