-- Stop API/workers before deploying. Additive and rerunnable; retain schema on app rollback.
-- Cutover: reserve ONLY runs active at deployment, using document PageCount. No terminal history backfill.
-- A billed active run is settled on maintenance or its next terminal transition.
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.JobRuns','U') IS NULL THROW 50001, 'Existing DocAi schema required.', 1;
IF COL_LENGTH('dbo.Consumers','QuotaMode') IS NULL
BEGIN
EXEC(N'ALTER TABLE dbo.Consumers ADD
    QuotaMode varchar(10) NOT NULL CONSTRAINT DF_Consumers_QuotaMode DEFAULT (''Unlimited''),
    QuotaWarningPercent tinyint NOT NULL CONSTRAINT DF_Consumers_QuotaWarning DEFAULT (10);');
-- A batch compiles before it runs: the CHECK needs the column from the previous EXEC.
EXEC(N'ALTER TABLE dbo.Consumers ADD CONSTRAINT CK_Consumers_QuotaMode CHECK (QuotaMode IN (''Unlimited'',''Prepaid'')),
    CONSTRAINT CK_Consumers_QuotaWarning CHECK (QuotaWarningPercent BETWEEN 1 AND 50);
ALTER TABLE dbo.JobRuns ADD CONSTRAINT UQ_Runs_Consumer UNIQUE (JobId,RunNo,ConsumerId);
CREATE TABLE dbo.PageAllowanceGrants (
    GrantId bigint IDENTITY NOT NULL CONSTRAINT PK_PageAllowanceGrants PRIMARY KEY,
    ConsumerId smallint NOT NULL CONSTRAINT FK_Allowance_Consumer REFERENCES dbo.Consumers(ConsumerId),
    Pages bigint NOT NULL,
    Kind varchar(10) NOT NULL,
    OriginalGrantId bigint NULL,
    OperatorIdentity nvarchar(200) NOT NULL,
    Note nvarchar(400) NOT NULL,
    RequestId uniqueidentifier NOT NULL CONSTRAINT UQ_Allowance_Request UNIQUE,
    RecordedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Allowance_Recorded DEFAULT SYSUTCDATETIME(),
    BalanceAfter bigint NOT NULL CONSTRAINT DF_Allowance_Balance DEFAULT (0),
    CONSTRAINT UQ_Allowance_Consumer UNIQUE (GrantId,ConsumerId),
    CONSTRAINT FK_Allowance_Original FOREIGN KEY (OriginalGrantId,ConsumerId) REFERENCES dbo.PageAllowanceGrants(GrantId,ConsumerId),
    CONSTRAINT CK_Allowance_Pages CHECK (Pages <> 0),
    CONSTRAINT CK_Allowance_Kind CHECK ((Kind=''TopUp'' AND Pages>0 AND OriginalGrantId IS NULL)
        OR Kind=''Adjustment'' OR (Kind=''Reversal'' AND OriginalGrantId IS NOT NULL)),
    CONSTRAINT CK_Allowance_Audit CHECK (LEN(OperatorIdentity)>0 AND LEN(Note)>0)
);
CREATE INDEX IX_Allowance_Consumer ON dbo.PageAllowanceGrants(ConsumerId,RecordedAtUtc,GrantId) INCLUDE(Pages,Kind,BalanceAfter);
CREATE TABLE dbo.PageReservations (
    JobId uniqueidentifier NOT NULL,
    RunNo smallint NOT NULL,
    ConsumerId smallint NOT NULL,
    Pages bigint NOT NULL,
    State varchar(10) NOT NULL CONSTRAINT DF_PageReservations_State DEFAULT (''Reserved''),
    ReservedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_PageReservations_Reserved DEFAULT SYSUTCDATETIME(),
    SettledAtUtc datetime2(3) NULL,
    OperationId bigint NULL CONSTRAINT FK_Reservation_Operation REFERENCES dbo.AzureOperations(OperationId),
    CONSTRAINT PK_PageReservations PRIMARY KEY(JobId,RunNo),
    CONSTRAINT UQ_Reservation_Consumer UNIQUE(JobId,RunNo,ConsumerId),
    CONSTRAINT FK_Reservation_Run FOREIGN KEY(JobId,RunNo,ConsumerId) REFERENCES dbo.JobRuns(JobId,RunNo,ConsumerId),
    CONSTRAINT CK_Reservation_Pages CHECK(Pages>0),
    CONSTRAINT CK_Reservation_State CHECK
        ((State=''Reserved'' AND SettledAtUtc IS NULL AND OperationId IS NULL)
        OR (State=''Charged'' AND SettledAtUtc IS NOT NULL AND OperationId IS NOT NULL)
        OR (State=''Released'' AND SettledAtUtc IS NOT NULL AND OperationId IS NULL))
);
CREATE INDEX IX_Reservations_Balance ON dbo.PageReservations(ConsumerId,State) INCLUDE(Pages);
CREATE TABLE dbo.CustomerPageCharges (
    ChargeId bigint IDENTITY NOT NULL CONSTRAINT PK_CustomerPageCharges PRIMARY KEY,
    JobId uniqueidentifier NOT NULL,
    RunNo smallint NOT NULL,
    ConsumerId smallint NOT NULL,
    Pages bigint NOT NULL,
    UsageId bigint NOT NULL CONSTRAINT FK_PageCharge_Usage REFERENCES dbo.UsageLedger(UsageId),
    OperationId bigint NOT NULL CONSTRAINT FK_PageCharge_Operation REFERENCES dbo.AzureOperations(OperationId),
    RecordedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_PageCharge_Recorded DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_PageCharge_Run UNIQUE(JobId,RunNo),
    CONSTRAINT FK_PageCharge_Reservation FOREIGN KEY(JobId,RunNo,ConsumerId) REFERENCES dbo.PageReservations(JobId,RunNo,ConsumerId),
    CONSTRAINT CK_PageCharge_Pages CHECK(Pages>0)
);
CREATE INDEX IX_PageCharges_Consumer ON dbo.CustomerPageCharges(ConsumerId,RecordedAtUtc) INCLUDE(Pages);
');
END;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Consumers')
    AND name='QuotaMode' AND system_type_id=167 AND max_length=10 AND is_nullable=0)
    THROW 50001, 'Incompatible Consumers.QuotaMode metadata.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Consumers')
    AND name='QuotaWarningPercent' AND system_type_id=48 AND is_nullable=0)
    THROW 50001, 'Incompatible Consumers.QuotaWarningPercent metadata.', 1;
EXEC(N'CREATE OR ALTER VIEW dbo.ConsumerPageBalance AS
SELECT c.ConsumerId,
    COALESCE(g.Pages,CONVERT(bigint,0))-COALESCE(ch.Pages,CONVERT(bigint,0))-COALESCE(r.Pages,CONVERT(bigint,0)) AS AvailablePages
FROM dbo.Consumers c
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.PageAllowanceGrants WHERE ConsumerId=c.ConsumerId) g
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.CustomerPageCharges WHERE ConsumerId=c.ConsumerId) ch
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.PageReservations WHERE ConsumerId=c.ConsumerId AND State=''Reserved'') r;');
EXEC(N'CREATE OR ALTER PROCEDURE dbo.AcquirePageQuotaLock @ConsumerId smallint AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 50016, ''Page quota lock requires a transaction.'', 1;
    DECLARE @resource nvarchar(255)=CONCAT(''DocAi.PageQuota.'',@ConsumerId), @rc int;
    EXEC @rc=sys.sp_getapplock @Resource=@resource,@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
    IF @rc<0 THROW 50016, ''Page quota lock could not be acquired.'', 1;
END;');
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
              WHERE i.Kind=''Reversal'' AND (i.Pages<>-g.Pages OR i.ConsumerId<>g.ConsumerId))
        THROW 50017, ''Invalid allowance reversal.'', 1;
    INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId,BalanceAfter)
    SELECT i.ConsumerId,i.Pages,i.Kind,i.OriginalGrantId,i.OperatorIdentity,i.Note,i.RequestId,b.AvailablePages+i.Pages
    FROM inserted i JOIN dbo.ConsumerPageBalance b ON b.ConsumerId=i.ConsumerId;
END;');
EXEC(N'CREATE OR ALTER PROCEDURE dbo.SettlePageReservation @JobId uniqueidentifier,@RunNo smallint AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @consumerId smallint, @status varchar(12), @pages bigint, @usageId bigint, @operationId bigint;
    SELECT @consumerId=ConsumerId FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=@RunNo;
    DECLARE @resource nvarchar(255)=CONCAT(''DocAi.PageQuota.'',@consumerId);
    IF @@TRANCOUNT=0 OR APPLOCK_MODE(''public'',@resource,''Transaction'')<>''Exclusive''
        THROW 50016, ''Consumer quota lock must precede settlement.'', 1;
    SELECT @status=Status,@pages=PageCount FROM dbo.JobRuns WITH(UPDLOCK,HOLDLOCK)
    WHERE JobId=@JobId AND RunNo=@RunNo;
    SELECT TOP(1) @usageId=UsageId,@operationId=OperationId FROM dbo.UsageLedger
    WHERE JobId=@JobId AND RunNo=@RunNo AND ReversesUsageId IS NULL ORDER BY UsageId;
    IF @usageId IS NOT NULL
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM dbo.PageReservations WHERE JobId=@JobId AND RunNo=@RunNo) RETURN;
        IF NOT EXISTS(SELECT 1 FROM dbo.CustomerPageCharges WHERE JobId=@JobId AND RunNo=@RunNo)
            INSERT dbo.CustomerPageCharges(JobId,RunNo,ConsumerId,Pages,UsageId,OperationId)
            VALUES(@JobId,@RunNo,@consumerId,@pages,@usageId,@operationId);
        UPDATE dbo.PageReservations SET State=''Charged'',SettledAtUtc=SYSUTCDATETIME(),OperationId=@operationId
        WHERE JobId=@JobId AND RunNo=@RunNo AND State<>''Charged'';
    END
    ELSE IF @status<>''Active''
    BEGIN
        UPDATE dbo.PageReservations SET State=''Released'',SettledAtUtc=SYSUTCDATETIME()
        WHERE JobId=@JobId AND RunNo=@RunNo AND State=''Reserved'';
        -- Fixed operational signal, no document/customer values. C# maps this to an error log event.
        IF @status=''Completed'' AND @@ROWCOUNT>0
            RAISERROR(''page_quota.completed_without_billed_usage'',10,1);
    END;
END;');
EXEC(N'CREATE OR ALTER PROCEDURE dbo.ReconcilePageReservations AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @consumerId smallint,@jobId uniqueidentifier,@runNo smallint;
    DECLARE consumers CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT ConsumerId FROM dbo.PageReservations WHERE State=''Reserved'';
    OPEN consumers;
    FETCH NEXT FROM consumers INTO @consumerId;
    WHILE @@FETCH_STATUS=0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            EXEC dbo.AcquirePageQuotaLock @consumerId;
            DECLARE runs CURSOR LOCAL STATIC READ_ONLY FOR
                SELECT r.JobId,r.RunNo FROM dbo.JobRuns r JOIN dbo.PageReservations p ON p.JobId=r.JobId AND p.RunNo=r.RunNo
                WHERE p.ConsumerId=@consumerId AND p.State=''Reserved'' AND
                (r.Status<>''Active'' OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.JobId=r.JobId AND l.RunNo=r.RunNo AND l.ReversesUsageId IS NULL));
            OPEN runs;
            FETCH NEXT FROM runs INTO @jobId,@runNo;
            WHILE @@FETCH_STATUS=0
            BEGIN
                EXEC dbo.SettlePageReservation @jobId,@runNo;
                FETCH NEXT FROM runs INTO @jobId,@runNo;
            END;
            CLOSE runs; DEALLOCATE runs;
            COMMIT;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK;
            THROW;
        END CATCH;
        FETCH NEXT FROM consumers INTO @consumerId;
    END;
    CLOSE consumers; DEALLOCATE consumers;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants INSTEAD OF UPDATE, DELETE AS BEGIN THROW 50017, ''Page accounting is append only.'', 1; END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_PageCharges_AppendOnly ON dbo.CustomerPageCharges INSTEAD OF UPDATE, DELETE AS BEGIN THROW 50017, ''Page accounting is append only.'', 1; END;');

EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Reservation_Integrity ON dbo.PageReservations AFTER INSERT,UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.JobRuns r ON r.JobId=i.JobId AND r.RunNo=i.RunNo
              WHERE i.ConsumerId<>r.ConsumerId OR i.Pages<>r.PageCount)
        THROW 50017, ''Reservation must match its run.'', 1;
    IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.JobId=d.JobId AND i.RunNo=d.RunNo
        WHERE i.ConsumerId<>d.ConsumerId OR i.Pages<>d.Pages OR i.ReservedAtUtc<>d.ReservedAtUtc
           OR (d.State<>''Reserved'' AND (i.State<>d.State OR ISNULL(i.OperationId,0)<>ISNULL(d.OperationId,0)
                                      OR i.SettledAtUtc<>d.SettledAtUtc)))
        THROW 50017, ''Settled reservations are immutable.'', 1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_PageCharge_Integrity ON dbo.CustomerPageCharges AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageReservations r ON r.JobId=i.JobId AND r.RunNo=i.RunNo
        JOIN dbo.UsageLedger l ON l.UsageId=i.UsageId JOIN dbo.AzureOperations o ON o.OperationId=i.OperationId
        WHERE i.Pages<>r.Pages OR i.ConsumerId<>r.ConsumerId OR l.ConsumerId<>i.ConsumerId OR l.JobId<>i.JobId
            OR l.RunNo<>i.RunNo OR l.OperationId<>i.OperationId OR l.ReversesUsageId IS NOT NULL
            OR o.JobId<>i.JobId OR o.RunNo<>i.RunNo OR o.State<>''Succeeded'')
        THROW 50017, ''Customer charge must match billed run usage.'', 1;
END;');

-- Stop-the-world cutover: repeated deployment never adds reservations for historical terminal runs.
EXEC(N'INSERT dbo.PageReservations(JobId,RunNo,ConsumerId,Pages)
SELECT r.JobId,r.RunNo,r.ConsumerId,r.PageCount FROM dbo.JobRuns r
WHERE r.Status=''Active'' AND NOT EXISTS(SELECT 1 FROM dbo.PageReservations p WHERE p.JobId=r.JobId AND p.RunNo=r.RunNo);
');
IF DATABASE_PRINCIPAL_ID('docai_runtime') IS NOT NULL
BEGIN
    GRANT SELECT ON dbo.PageAllowanceGrants TO docai_runtime;
    DENY INSERT,UPDATE,DELETE ON dbo.PageAllowanceGrants TO docai_runtime;
    GRANT INSERT,UPDATE ON dbo.PageReservations TO docai_runtime;
    GRANT INSERT ON dbo.CustomerPageCharges TO docai_runtime;
    DENY UPDATE,DELETE ON dbo.CustomerPageCharges TO docai_runtime;
    GRANT EXECUTE ON dbo.AcquirePageQuotaLock TO docai_runtime;
    GRANT EXECUTE ON dbo.SettlePageReservation TO docai_runtime;
    GRANT EXECUTE ON dbo.ReconcilePageReservations TO docai_runtime;
END;

-- Stage 4: durable replay identity for mode/warning changes as well as grants.
IF OBJECT_ID('dbo.PageQuotaOperatorRequests','U') IS NULL
CREATE TABLE dbo.PageQuotaOperatorRequests(
 RequestId uniqueidentifier NOT NULL PRIMARY KEY,
 ConsumerId smallint NOT NULL REFERENCES dbo.Consumers(ConsumerId),
 Payload nvarchar(1200) NOT NULL,
 RecordedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
IF DATABASE_PRINCIPAL_ID('docai_quota_operator') IS NULL CREATE ROLE docai_quota_operator;
GRANT SELECT,INSERT ON dbo.PageQuotaOperatorRequests TO docai_quota_operator;
DENY UPDATE,DELETE ON dbo.PageQuotaOperatorRequests TO docai_quota_operator;
GRANT INSERT,SELECT ON dbo.PageAllowanceGrants TO docai_quota_operator;
GRANT UPDATE(QuotaMode,QuotaWarningPercent) ON dbo.Consumers TO docai_quota_operator;
GRANT SELECT ON dbo.Consumers TO docai_quota_operator;
GRANT SELECT ON dbo.ConsumerPageBalance TO docai_quota_operator;
GRANT SELECT ON dbo.PageReservations TO docai_quota_operator;
GRANT SELECT ON dbo.CustomerPageCharges TO docai_quota_operator;
GRANT SELECT ON dbo.JobRuns TO docai_quota_operator;
GRANT EXECUTE ON dbo.AcquirePageQuotaLock TO docai_quota_operator;
COMMIT;
END TRY
BEGIN CATCH
IF XACT_STATE()<>0 ROLLBACK;
THROW;
END CATCH;
