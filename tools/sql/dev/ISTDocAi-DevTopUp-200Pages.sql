/* =====================================================================
   DEVELOPMENT ONLY - add 200 pages to the local dev consumer (FWD-UNF).

   Why: the dev seed opens the consumer with 100 Prepaid pages. When the
   balance is lower than a document's page count, a re-read after a model
   upgrade is skipped and the kit shows the previous result
   ("the page balance is not enough to re-read it with the updated model").

   How to use (SSMS):
     1. Connect to your LOCAL DocAi service database (the one the dev seed
        and the model upgrade script ran on).
     2. Run the whole script once (F5). Result = COMMITTED.
     Safe to re-run: the fixed @RequestId means pages are never added twice
     (Result = ALREADY APPLIED). For another top-up later, put a NEW
     @RequestId (SELECT NEWID();).

   Same rules as tools/sql/operator/TopUp-Pages.sql and the OperatorTool:
   consumer lock, append-only grants, same retry identity
   (PageQuotaOperatorRequests). Never run this on a production database.
   ===================================================================== */

------------------------------ Settings ------------------------------
DECLARE @ConsumerCode varchar(30)       = 'FWD-UNF';
DECLARE @Pages        bigint            = 200;
DECLARE @Note         nvarchar(400)     = N'Development top-up 200 pages';
DECLARE @RequestId    uniqueidentifier  = 'A6CD7499-DC76-4AD4-BE5E-2CD2DF30008A';
DECLARE @Apply        bit               = 1;   -- 0 = preview only (rollback), 1 = commit
-----------------------------------------------------------------------

SET NOCOUNT ON; SET XACT_ABORT ON;
IF OBJECT_ID('dbo.PageAllowanceGrants','U') IS NULL OR OBJECT_ID('dbo.PageQuotaOperatorRequests','U') IS NULL
    THROW 50018, 'Wrong database: DocAi page quota tables not found. Select your local DocAi database.', 1;
IF @Pages < 1 OR @Pages > 1000000 THROW 50018, 'TopUp Pages must be 1..1000000.', 1;
IF @RequestId IS NULL OR @RequestId = '00000000-0000-0000-0000-000000000000' THROW 50018, 'Set @RequestId (SELECT NEWID()).', 1;
IF @Note IS NULL OR LEN(LTRIM(RTRIM(@Note))) = 0 OR LEN(@Note) > 400 THROW 50018, 'Note of 1..400 characters required.', 1;

-- Same audit identity and payload format as the OperatorTool, so a re-run is recognised as a replay.
DECLARE @Operator nvarchar(200) = LEFT(CONCAT(SUSER_SNAME(), N'@', HOST_NAME()), 200);
DECLARE @Payload nvarchar(1200) = CONCAT(N'TopUp|', @Pages, N'|0||10|0|0|0|', @Operator, N'|', @Note);

BEGIN TRY
BEGIN TRANSACTION;
DECLARE @id smallint = (SELECT ConsumerId FROM dbo.Consumers WHERE Code = @ConsumerCode);
IF @id IS NULL THROW 50018, 'Consumer FWD-UNF not found. Run the dev seed first.', 1;
EXEC dbo.AcquirePageQuotaLock @id;
IF NOT EXISTS (SELECT 1 FROM dbo.Consumers WHERE ConsumerId = @id AND IsActive = 1) THROW 50018, 'Consumer inactive.', 1;

DECLARE @replay bit = 0;
IF EXISTS (SELECT 1 FROM dbo.PageQuotaOperatorRequests WHERE RequestId = @RequestId)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.PageQuotaOperatorRequests
                   WHERE RequestId = @RequestId AND ConsumerId = @id AND Payload = @Payload)
        THROW 50018, 'RequestId already used for a different request; no changes. Use a NEW @RequestId.', 1;
    SET @replay = 1;
END;
IF @replay = 0 AND EXISTS (SELECT 1 FROM dbo.PageAllowanceGrants WHERE RequestId = @RequestId)
    THROW 50018, 'RequestId already belongs to an allowance grant. Use a NEW @RequestId.', 1;

DECLARE @before bigint = (SELECT AvailablePages FROM dbo.ConsumerPageBalance WHERE ConsumerId = @id);

IF @replay = 0
BEGIN
    -- The INSTEAD OF trigger takes the consumer lock again and records BalanceAfter.
    INSERT dbo.PageAllowanceGrants (ConsumerId, Pages, Kind, OriginalGrantId, OperatorIdentity, Note, RequestId)
    VALUES (@id, @Pages, 'TopUp', NULL, @Operator, @Note, @RequestId);
    INSERT dbo.PageQuotaOperatorRequests (RequestId, ConsumerId, Payload) VALUES (@RequestId, @id, @Payload);
END;

SELECT CASE WHEN @replay = 1 THEN 'ALREADY APPLIED earlier (no pages added)'
            WHEN @Apply = 1 THEN 'COMMITTED'
            ELSE 'PREVIEW ONLY - rolled back. Set @Apply = 1 to commit.' END AS Result,
       c.Code, c.QuotaMode,
       @before AS RemainingBefore,
       b.AvailablePages AS RemainingAfter
FROM dbo.Consumers c JOIN dbo.ConsumerPageBalance b ON b.ConsumerId = c.ConsumerId
WHERE c.ConsumerId = @id;

IF @Apply = 1 COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
