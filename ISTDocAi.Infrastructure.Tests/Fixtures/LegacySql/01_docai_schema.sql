-- =====================================================================================================

-- DocAi database (Azure SQL, Microsoft Entra-only authentication)

-- Applied by DbUp, run by the pipeline identity. Users are created once per environment by an Entra admin

-- (00_bootstrap_identities.sql); permissions are granted by 02_docai_grants.sql. This file creates tables only.

-- =====================================================================================================



-- -----------------------------------------------------------------------------------------------------

-- Consumers: one row per deployed application per company (Forwarding for UNF, Forwarding for CPW, ...)

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.Consumers (

    ConsumerId          smallint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Consumers PRIMARY KEY,

    Code                varchar(30)    NOT NULL CONSTRAINT UQ_Consumers_Code UNIQUE,   -- 'FWD-UNF' = product + tenant

    Name                nvarchar(200)  NOT NULL,

    ProductCode         varchar(20)    NOT NULL,   -- 'FWD', 'YARD': cost roll-up per product

    -- 'UNF', 'CPW': same code as Defaults.UnEditableCompanyName and Plan E

    TenantCode          varchar(20)    NOT NULL,

    ApiKeyHash1         binary(32)     NOT NULL,   -- SHA-256 of the active API key

    ApiKeyHash2         binary(32)     NULL,       -- second slot, used only during a rotation

    CallbackUrl         nvarchar(400)  NULL,       -- registered once; the request body can't change it (no SSRF).

                                                   -- NULL: the consumer has no inbound access and only polls

    WebhookSecretName   varchar(127)   NULL,       -- Key Vault secret NAME, never the secret

    -- optional: Key Vault secret NAME holding the PEM certificate (public part only) of a private CA trusted for

    -- this callback only, never a blanket bypass. A secret, because a Key Vault certificate must carry its key

    CallbackCaCertName  varchar(127)   NULL,

    MaxPagesPerJob      smallint       NOT NULL CONSTRAINT DF_Consumers_MaxPages DEFAULT (10),

    DailyPageCap        int            NOT NULL CONSTRAINT DF_Consumers_DailyCap DEFAULT (2000),

    IsActive            bit            NOT NULL CONSTRAINT DF_Consumers_IsActive DEFAULT (1),

    CreatedAtUtc        datetime2(0)   NOT NULL CONSTRAINT DF_Consumers_Created DEFAULT (SYSUTCDATETIME()),

    RowVersion          rowversion     NOT NULL,

    CONSTRAINT UQ_Consumers_Product_Tenant UNIQUE (ProductCode, TenantCode),

    CONSTRAINT CK_Consumers_Callback CHECK ((CallbackUrl IS NULL AND WebhookSecretName IS NULL)

                                         OR (CallbackUrl LIKE 'https://%' AND WebhookSecretName IS NOT NULL)),

    CONSTRAINT CK_Consumers_CaCert   CHECK (CallbackCaCertName IS NULL OR CallbackUrl IS NOT NULL),

    CONSTRAINT CK_Consumers_Limits   CHECK (MaxPagesPerJob BETWEEN 1 AND 2000 AND DailyPageCap > 0)

);

CREATE UNIQUE INDEX UX_Consumers_Key1 ON dbo.Consumers (ApiKeyHash1);

CREATE UNIQUE INDEX UX_Consumers_Key2 ON dbo.Consumers (ApiKeyHash2) WHERE ApiKeyHash2 IS NOT NULL;

GO



-- -----------------------------------------------------------------------------------------------------

-- ModelRegistry: which Document Intelligence model serves (document type, shipping line).

-- For a classifier row, MinFieldConfidence is the minimum class confidence.

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.ModelRegistry (

    ModelId             varchar(64)   NOT NULL CONSTRAINT PK_ModelRegistry PRIMARY KEY,   -- 'bl-maersk-v1'

    DocumentTypeCode    varchar(20)   NOT NULL,   -- 'BL'

    -- 'MAERSK'. NULL: the document type's generic extraction model (any line), or its classifier

    ShippingLineCode    varchar(20)   NULL,

    Kind                varchar(10)   NOT NULL,   -- extract | classify (decides the billing meter)

    Version             smallint      NOT NULL,

    Status              varchar(10)   NOT NULL CONSTRAINT DF_Model_Status DEFAULT ('Draft'),

    MinFieldConfidence  decimal(4,3)  NOT NULL CONSTRAINT DF_Model_MinConf DEFAULT (0.800),

    TestFieldAccuracy   decimal(5,4)  NULL,       -- measured on the held-out test set before activation

    ActivatedAtUtc      datetime2(0)  NULL,

    RetiredAtUtc        datetime2(0)  NULL,

    RowVersion          rowversion    NOT NULL,

    CONSTRAINT CK_Model_Kind   CHECK (Kind IN ('extract', 'classify')),

    CONSTRAINT CK_Model_Status CHECK (Status IN ('Draft', 'Testing', 'Active', 'Retired')),

    -- an extraction model serves one line, or (line NULL) the whole document type; a classifier has no line

    CONSTRAINT CK_Model_Line   CHECK (Kind = 'extract' OR ShippingLineCode IS NULL),

    CONSTRAINT UQ_Model_Version UNIQUE (DocumentTypeCode, ShippingLineCode, Kind, Version)

);

-- At most one Active model per (document type, shipping line, kind); NULL lines compare equal here, so there is

-- also at most one Active generic model and one Active classifier per document type. Activation is one

-- transaction ([S14]).

CREATE UNIQUE INDEX UX_Model_OneActive ON dbo.ModelRegistry (DocumentTypeCode, ShippingLineCode, Kind)

    WHERE Status = 'Active';

GO



-- -----------------------------------------------------------------------------------------------------

-- ClassifierClasses: the class names a classifier was trained with, mapped to shipping lines.

-- Filled by the model team before the classifier is activated ([S14] refuses a classifier without classes).

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.ClassifierClasses (

    ClassifierModelId   varchar(64)   NOT NULL CONSTRAINT FK_Class_Model REFERENCES dbo.ModelRegistry (ModelId),

    ClassName           varchar(64)   NOT NULL,   -- the docType name in the classifier's analyzeResult

    ShippingLineCode    varchar(20)   NULL,

    TargetDocumentTypeCode varchar(20) NULL,

    CONSTRAINT CK_Class_Target CHECK (TargetDocumentTypeCode IS NULL OR TargetDocumentTypeCode <> 'AUTO'),

    CONSTRAINT CK_Class_TargetLine CHECK (TargetDocumentTypeCode IS NOT NULL OR ShippingLineCode IS NULL),

    CONSTRAINT PK_ClassifierClasses PRIMARY KEY (ClassifierModelId, ClassName)

);

GO



-- -----------------------------------------------------------------------------------------------------

-- Jobs: the request, its state machine, and the queue itself (Phase 1 has no separate queue).

-- JobSeq is the clustered key so inserts append; JobId (any GUID) is the public, nonclustered primary key.

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.Jobs (

    JobSeq                     bigint IDENTITY(1,1) NOT NULL,

    JobId                      uniqueidentifier NOT NULL,

    ConsumerId                 smallint         NOT NULL

                               CONSTRAINT FK_Jobs_Consumers REFERENCES dbo.Consumers (ConsumerId),

    RunNo                      smallint         NOT NULL CONSTRAINT DF_Jobs_RunNo DEFAULT (1),   -- current run

    ClientRequestId            varchar(100)     NULL,      -- the current run's clientRequestId (AiDocuments.LocalId)

    DocumentTypeCode           varchar(20)      NOT NULL,

    RequestedShippingLineCode  varchar(20)      NULL,      -- as sent by the consumer; never changes for this job

    IdemLine                   varchar(20)      NOT NULL,  -- COALESCE(RequestedShippingLineCode, '*'): in the key

    ShippingLineCode           varchar(20)      NULL,      -- the line in use: requested, or found by the classifier

    -- computed by the service from the bytes, never taken from the client

    FileSha256                 binary(32)       NOT NULL,

    FileName                   nvarchar(260)    NULL,      -- display only, never a path; cleared by retention

    ContentType                varchar(100)     NOT NULL,  -- from the magic bytes, not from the request header

    FileSizeBytes              int              NOT NULL,

    PageCount                  smallint         NOT NULL,

    BlobPath                   varchar(400)     NOT NULL,  -- documents/{consumerCode}/{yyyy}/{MM}/{uploadId}.{ext}

    ModelId                    varchar(64)      NULL CONSTRAINT FK_Jobs_Model REFERENCES dbo.ModelRegistry (ModelId),

    Stage                      varchar(10)      NOT NULL,  -- Classify | Extract | Map (Azure done, mapping left)

    Status                     varchar(12)      NOT NULL CONSTRAINT DF_Jobs_Status DEFAULT ('Queued'),

    Attempts                   tinyint          NOT NULL CONSTRAINT DF_Jobs_Attempts DEFAULT (0),

    TakeoverCount              tinyint          NOT NULL CONSTRAINT DF_Jobs_Takeovers DEFAULT (0),

    NextAttemptAtUtc           datetime2(3)     NOT NULL CONSTRAINT DF_Jobs_Next DEFAULT (SYSUTCDATETIME()),

    LeaseToken                 uniqueidentifier NULL,      -- new on every claim; every later write checks it

    LeaseExpiresAtUtc          datetime2(3)     NULL,

    ErrorCode                  varchar(50)      NULL,

    ErrorMessage               nvarchar(400)    NULL,      -- written by the service, never extracted values

    CorrelationId              varchar(64)      NOT NULL,

    CreatedAtUtc               datetime2(3)     NOT NULL CONSTRAINT DF_Jobs_Created DEFAULT (SYSUTCDATETIME()),

    StartedAtUtc               datetime2(3)     NULL,

    CompletedAtUtc             datetime2(3)     NULL,

    IsDeleted                  bit              NOT NULL CONSTRAINT DF_Jobs_IsDeleted DEFAULT (0),   -- retention

    RowVersion                 rowversion       NOT NULL,

    ResolvedDocumentTypeCode   varchar(20)      NULL,

    CONSTRAINT CK_Jobs_ResolvedType CHECK (ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> 'AUTO'),

    CONSTRAINT PK_Jobs PRIMARY KEY NONCLUSTERED (JobId),

    CONSTRAINT UQ_Jobs_Seq UNIQUE CLUSTERED (JobSeq),

    CONSTRAINT CK_Jobs_Status   CHECK (Status IN ('Queued', 'Processing', 'Completed', 'Failed')),

    CONSTRAINT CK_Jobs_Stage    CHECK (Stage IN ('Classify', 'Extract', 'Map')),

    CONSTRAINT CK_Jobs_Attempts CHECK (Attempts <= 5),

    CONSTRAINT CK_Jobs_Pages    CHECK (PageCount > 0),

    CONSTRAINT CK_Jobs_RunNo    CHECK (RunNo >= 1),

    CONSTRAINT CK_Jobs_IdemLine CHECK (IdemLine = COALESCE(RequestedShippingLineCode, '*')),

    CONSTRAINT CK_Jobs_Lease    CHECK ((Status = 'Processing' AND LeaseToken IS NOT NULL)

                                    OR (Status <> 'Processing' AND LeaseToken IS NULL))

);

-- Idempotency: the same consumer + document type + file + requested line is the same job.

CREATE UNIQUE INDEX UX_Jobs_Idempotency ON dbo.Jobs (ConsumerId, DocumentTypeCode, FileSha256, IdemLine)

    WHERE IsDeleted = 0;

-- Queue scan: only live rows are in this index, so it stays small however many jobs have completed.

CREATE INDEX IX_Jobs_Queue ON dbo.Jobs (Status, NextAttemptAtUtc) INCLUDE (LeaseExpiresAtUtc)

    WHERE Status IN ('Queued', 'Processing') AND IsDeleted = 0;

CREATE INDEX IX_Jobs_Consumer_Created ON dbo.Jobs (ConsumerId, CreatedAtUtc) INCLUDE (PageCount, Status);

GO



-- -----------------------------------------------------------------------------------------------------

-- JobRuns: one row per authorized analysis of a job — the first submission, the same file sent again after

-- a failure, or an explicit reprocess. Retries, back-offs and lease takeovers never create a run.

-- Each run keeps its own outcome, so a consumer bound to an older run can still read it ([S13]).

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.JobRuns (
    MappingFingerprint char(64) NULL,
    ChangesJson nvarchar(max) NULL,

    JobId            uniqueidentifier NOT NULL CONSTRAINT FK_Runs_Jobs REFERENCES dbo.Jobs (JobId),

    RunNo            smallint         NOT NULL,

    Reason           varchar(12)      NOT NULL,   -- New | Requeue | Reprocess | ModelChanged

    ConsumerId       smallint         NOT NULL CONSTRAINT FK_Runs_Consumers REFERENCES dbo.Consumers (ConsumerId),

    PageCount        smallint         NOT NULL,   -- counted by the daily page cap ([S1])

    ClientRequestId  varchar(100)     NULL,       -- the submission that started the run

    CorrelationId    varchar(64)      NOT NULL,

    Status           varchar(12)      NOT NULL CONSTRAINT DF_Runs_Status DEFAULT ('Active'),   -- Active | Completed | Failed

    ErrorCode        varchar(50)      NULL,

    ErrorMessage     nvarchar(400)    NULL,

    CreatedAtUtc     datetime2(3)     NOT NULL CONSTRAINT DF_Runs_Created DEFAULT (SYSUTCDATETIME()),

    CompletedAtUtc   datetime2(3)     NULL,

    ResolvedDocumentTypeCode varchar(20) NULL,

    ResultType       varchar(32)      NULL,

    CONSTRAINT CK_Runs_ResolvedType CHECK (ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> 'AUTO'),

    CONSTRAINT PK_JobRuns PRIMARY KEY (JobId, RunNo),

    CONSTRAINT CK_Runs_Reason CHECK (Reason IN ('New', 'Requeue', 'Reprocess', 'ModelChanged')),

    CONSTRAINT CK_Runs_Status CHECK (Status IN ('Active', 'Completed', 'Failed'))

);

CREATE INDEX IX_Runs_Consumer_Created ON dbo.JobRuns (ConsumerId, CreatedAtUtc) INCLUDE (PageCount);

GO



-- -----------------------------------------------------------------------------------------------------

-- JobSubmissions: every submission a consumer made, bound for good to the job and run it was answered with —

-- including a submission answered "Existing". The same clientRequestId sent again (a lost reply, a resend)

-- always gets that same job and run back, so a resend can never start a second billed run.

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.JobSubmissions (

    ConsumerId       smallint         NOT NULL CONSTRAINT FK_Sub_Consumers REFERENCES dbo.Consumers (ConsumerId),

    ClientRequestId  varchar(100)     NOT NULL,   -- AiDocuments.LocalId

    JobId            uniqueidentifier NOT NULL,

    RunNo            smallint         NOT NULL,

    CreatedAtUtc     datetime2(3)     NOT NULL CONSTRAINT DF_Sub_Created DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_JobSubmissions PRIMARY KEY (ConsumerId, ClientRequestId),

    CONSTRAINT FK_Sub_Runs FOREIGN KEY (JobId, RunNo) REFERENCES dbo.JobRuns (JobId, RunNo)

);

GO



-- -----------------------------------------------------------------------------------------------------

-- AzureOperations: one row per call to Document Intelligence, written BEFORE the POST.

-- Sending -> Accepted (202, location saved) -> Succeeded | Failed. Unknown: no answer, Azure may have billed it.

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.AzureOperations (

    OperationId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AzureOperations PRIMARY KEY,

    JobId               uniqueidentifier NOT NULL,

    RunNo               smallint         NOT NULL,

    Kind                varchar(10)      NOT NULL,   -- classify | extract

    ModelId             varchar(64)      NOT NULL CONSTRAINT FK_Op_Model REFERENCES dbo.ModelRegistry (ModelId),

    State               varchar(10)      NOT NULL CONSTRAINT DF_Op_State DEFAULT ('Sending'),

    OperationLocation   nvarchar(500)    NULL,

    Pages               int              NULL,       -- pages Azure analyzed (from analyzeResult)

    ClassName           varchar(64)      NULL,       -- classify only

    ClassConfidence     decimal(4,3)     NULL,

    -- extract only: the raw analyzeResult saved to Blob before mapping, so mapping can resume after a crash

    RawResultPath       varchar(400)     NULL,       -- results/{jobId}/{runNo}/{operationId}.json

    AzureErrorCode      varchar(50)      NULL,

    SentAtUtc           datetime2(3)     NOT NULL CONSTRAINT DF_Op_Sent DEFAULT (SYSUTCDATETIME()),

    AcceptedAtUtc       datetime2(3)     NULL,

    CompletedAtUtc      datetime2(3)     NULL,       -- Azure keeps the result 24 hours from here

    ResultDeletedAtUtc  datetime2(3)     NULL,

    CONSTRAINT FK_Op_Run FOREIGN KEY (JobId, RunNo) REFERENCES dbo.JobRuns (JobId, RunNo),

    CONSTRAINT CK_Op_Kind     CHECK (Kind IN ('classify', 'extract')),

    CONSTRAINT CK_Op_State    CHECK (State IN ('Sending', 'Accepted', 'Succeeded', 'Failed', 'Unknown')),

    CONSTRAINT CK_Op_Location CHECK (State NOT IN ('Accepted', 'Succeeded') OR OperationLocation IS NOT NULL),

    CONSTRAINT CK_Op_Pages    CHECK (State <> 'Succeeded' OR Pages > 0)

);

-- At most one open operation per run and kind: two workers can never both be submitting the same step.

CREATE UNIQUE INDEX UX_Op_Open ON dbo.AzureOperations (JobId, RunNo, Kind) WHERE State IN ('Sending', 'Accepted');

CREATE INDEX IX_Op_Unknown ON dbo.AzureOperations (SentAtUtc) INCLUDE (Kind) WHERE State = 'Unknown';

GO



-- -----------------------------------------------------------------------------------------------------

-- JobResults: the canonical JSON v1 of a run, kept apart so the queue rows stay narrow

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.JobResults (

    JobId            uniqueidentifier NOT NULL,

    RunNo            smallint         NOT NULL,

    ContractVersion  varchar(10)      NOT NULL,   -- '1.0'

    ResultJson       nvarchar(max)    NOT NULL CONSTRAINT CK_JobResults_Json CHECK (ISJSON(ResultJson) = 1),

    FlaggedFields    smallint         NOT NULL,   -- fields under the model's MinFieldConfidence or failing a check

    CreatedAtUtc     datetime2(3)     NOT NULL CONSTRAINT DF_JobResults_Created DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_JobResults PRIMARY KEY (JobId, RunNo),

    CONSTRAINT FK_JobResults_Runs FOREIGN KEY (JobId, RunNo) REFERENCES dbo.JobRuns (JobId, RunNo)

);

CREATE INDEX IX_JobResults_Created ON dbo.JobResults (CreatedAtUtc);

GO



-- -----------------------------------------------------------------------------------------------------

-- WebhookDeliveries: outbox for callbacks, written in the same transaction as the run's outcome.

-- Events are immutable: one row per (job, run, event type); a newer run supersedes older Pending rows.

-- PayloadJson holds the event only; the sender adds the run's result from JobResults when it sends ([S17]).

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.WebhookDeliveries (

    DeliveryId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WebhookDeliveries PRIMARY KEY,

    EventId            uniqueidentifier NOT NULL CONSTRAINT UQ_Webhook_EventId UNIQUE,

    JobId              uniqueidentifier NOT NULL,

    RunNo              smallint         NOT NULL,

    ConsumerId         smallint         NOT NULL

                       CONSTRAINT FK_Webhook_Consumers REFERENCES dbo.Consumers (ConsumerId),

    EventType          varchar(20)      NOT NULL,   -- job.completed | job.failed

    Status             varchar(12)      NOT NULL CONSTRAINT DF_Webhook_Status DEFAULT ('Pending'),

    PayloadJson        nvarchar(max)    NOT NULL,   -- eventId, eventType, jobId, runNo, clientRequestId, status, error

    Attempts           tinyint          NOT NULL CONSTRAINT DF_Webhook_Attempts DEFAULT (0),

    NextAttemptAtUtc   datetime2(3)     NOT NULL CONSTRAINT DF_Webhook_Next DEFAULT (SYSUTCDATETIME()),

    LeaseToken         uniqueidentifier NULL,

    LeaseExpiresAtUtc  datetime2(3)     NULL,

    LastStatusCode     smallint         NULL,

    LastError          nvarchar(500)    NULL,

    CreatedAtUtc       datetime2(3)     NOT NULL CONSTRAINT DF_Webhook_Created DEFAULT (SYSUTCDATETIME()),

    DeliveredAtUtc     datetime2(3)     NULL,

    CONSTRAINT FK_Webhook_Runs FOREIGN KEY (JobId, RunNo) REFERENCES dbo.JobRuns (JobId, RunNo),

    CONSTRAINT CK_Webhook_Status CHECK (Status IN ('Pending', 'Delivered', 'DeadLettered', 'Superseded')),

    CONSTRAINT CK_Webhook_Event  CHECK (EventType IN ('job.completed', 'job.failed')),

    CONSTRAINT CK_Webhook_Json   CHECK (ISJSON(PayloadJson) = 1),

    CONSTRAINT UQ_Webhook_Run_Event UNIQUE (JobId, RunNo, EventType)

);

CREATE INDEX IX_Webhook_Due ON dbo.WebhookDeliveries (NextAttemptAtUtc) INCLUDE (LeaseExpiresAtUtc)

    WHERE Status = 'Pending';

GO



-- -----------------------------------------------------------------------------------------------------

-- AzureMeterPrices: effective-dated list prices; the ledger copies the price in force when Azure finished

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.AzureMeterPrices (

    MeterCode      varchar(30)    NOT NULL,   -- DI_CUSTOM_EXTRACT | DI_CUSTOM_CLASSIFY

    EffectiveFrom  date           NOT NULL,

    PricePer1000   decimal(9,4)   NOT NULL,   -- USD per 1,000 pages, pay-as-you-go

    Currency       char(3)        NOT NULL CONSTRAINT DF_Meter_Currency DEFAULT ('USD'),

    Source         nvarchar(200)  NOT NULL,

    CONSTRAINT PK_AzureMeterPrices PRIMARY KEY (MeterCode, EffectiveFrom),

    CONSTRAINT CK_Meter_Code  CHECK (MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY')),

    CONSTRAINT CK_Meter_Price CHECK (PricePer1000 > 0)

);

INSERT dbo.AzureMeterPrices (MeterCode, EffectiveFrom, PricePer1000, Source) VALUES

    ('DI_CUSTOM_EXTRACT',  '2026-10-01', 30.0000, N'Azure Retail Prices API, France Central, read 2026-10-11'),

    ('DI_CUSTOM_CLASSIFY', '2026-10-01',  3.0000, N'Azure Retail Prices API, France Central, read 2026-10-11');

GO



-- -----------------------------------------------------------------------------------------------------

-- UsageLedger: append-only billing facts, one row per succeeded Azure operation, whatever the job's outcome.

-- Never UPDATE or DELETE a row (the runtime role cannot): a correction is a new, negative row.

-- PricePer1000 is NULL when no price was in force: the row is still recorded (the usage is a fact), the

-- "unpriced usage" alert fires until the price list has a price for that date ([B9]), and reports price it from

-- AzureMeterPrices by its date ([B1]). The month close allocates the actual bill by pages, so it never needs

-- the list price. A row always keeps the month Azure did the work in; one recorded after that month was closed

-- is late usage ([B8]).

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.UsageLedger (

    UsageId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_UsageLedger PRIMARY KEY,

    ConsumerId       smallint         NOT NULL CONSTRAINT FK_Usage_Consumers REFERENCES dbo.Consumers (ConsumerId),

    JobId            uniqueidentifier NOT NULL,

    RunNo            smallint         NOT NULL,

    OperationId      bigint           NOT NULL CONSTRAINT FK_Usage_Operations REFERENCES dbo.AzureOperations (OperationId),

    MeterCode        varchar(30)      NOT NULL,

    ModelId          varchar(64)      NOT NULL,

    Pages            int              NOT NULL,   -- pages Azure analyzed; negative only on a reversal

    PricePer1000     decimal(9,4)     NULL,       -- copied at write time: a later price change never rewrites history

    AzureCostUsd     AS (CONVERT(decimal(12,4), Pages * PricePer1000 / 1000)) PERSISTED,   -- NULL when unpriced

    OccurredAtUtc    datetime2(3)     NOT NULL,   -- when Azure finished the operation (a reversal: the original's)

    BillingPeriod    AS (CONVERT(char(7), OccurredAtUtc, 126)) PERSISTED,   -- 'YYYY-MM' in UTC, like Azure usage dates

    RecordedAtUtc    datetime2(3)     NOT NULL CONSTRAINT DF_Usage_Recorded DEFAULT (SYSUTCDATETIME()),

    ReversesUsageId  bigint           NULL CONSTRAINT FK_Usage_Reverses REFERENCES dbo.UsageLedger (UsageId),

    CorrelationId    varchar(64)      NOT NULL,

    CONSTRAINT FK_Usage_Runs FOREIGN KEY (JobId, RunNo) REFERENCES dbo.JobRuns (JobId, RunNo),

    CONSTRAINT CK_Usage_Meter CHECK (MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY')),

    CONSTRAINT CK_Usage_Sign  CHECK ((ReversesUsageId IS NULL     AND Pages > 0)

                                OR (ReversesUsageId IS NOT NULL AND Pages < 0))

);

-- One billing row per Azure operation, so a retried or replayed completion can't bill twice.

CREATE UNIQUE INDEX UX_Usage_Operation ON dbo.UsageLedger (OperationId) WHERE ReversesUsageId IS NULL;

-- One reversal per row, so two operators can't reverse the same charge twice.

CREATE UNIQUE INDEX UX_Usage_OneReversal ON dbo.UsageLedger (ReversesUsageId) WHERE ReversesUsageId IS NOT NULL;

CREATE INDEX IX_Usage_Period ON dbo.UsageLedger (BillingPeriod, ConsumerId)

    INCLUDE (MeterCode, Pages, AzureCostUsd, JobId);

-- The "unpriced usage" alert reads this index.

CREATE INDEX IX_Usage_Unpriced ON dbo.UsageLedger (OccurredAtUtc) INCLUDE (MeterCode, Pages)

    WHERE PricePer1000 IS NULL;

GO



-- -----------------------------------------------------------------------------------------------------

-- BillingPeriods: the month's actual Azure numbers, entered from Cost Management before the month is closed

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.BillingPeriods (

    BillingPeriod       char(7)        NOT NULL CONSTRAINT PK_BillingPeriods PRIMARY KEY,   -- 'YYYY-MM'

    -- custom extraction cost of the shared DI resource, after any commitment tier

    DiExtractBillUsd    decimal(12,2)  NOT NULL,

    DiClassifyBillUsd   decimal(12,2)  NOT NULL,

    AzureExtractPages   int            NOT NULL,   -- meter quantity converted to pages

    AzureClassifyPages  int            NOT NULL,

    -- everything else in rg-docai-prod: App Service, SQL, Storage, Key Vault, logs

    InfraCostUsd        decimal(12,2)  NOT NULL,

    -- dev DI resource: training hours, Studio tests

    ModelDevCostUsd     decimal(12,2)  NOT NULL CONSTRAINT DF_BP_ModelDev DEFAULT (0),

    ReconciledAtUtc     datetime2(0)   NULL,       -- set by [B3] when both meters are within 2 %: the gate for [B4]

    ReconciledBy        nvarchar(100)  NULL,

    -- [B3c]: pages Azure billed that the ledger can't attribute (a billed Unknown call), approved with a reason

    ApprovedExtractVariancePages   int     NOT NULL CONSTRAINT DF_BP_VarExtract  DEFAULT (0),

    ApprovedClassifyVariancePages  int     NOT NULL CONSTRAINT DF_BP_VarClassify DEFAULT (0),

    VarianceReason      nvarchar(400)  NULL,

    VarianceApprovedBy  nvarchar(100)  NULL,

    UnallocatedUsd      decimal(12,2)  NOT NULL CONSTRAINT DF_BP_Unallocated DEFAULT (0),   -- set by [B4]

    Status              varchar(10)    NOT NULL CONSTRAINT DF_BP_Status DEFAULT ('Open'),

    ClosedAtUtc         datetime2(3)   NULL,

    ClosedBy            nvarchar(100)  NULL,

    CONSTRAINT CK_BP_Period CHECK (BillingPeriod LIKE '[2][0-9][0-9][0-9]-[01][0-9]'),

    CONSTRAINT CK_BP_Status CHECK (Status IN ('Open', 'Closed')),

    CONSTRAINT CK_BP_Closed CHECK (Status = 'Open' OR ReconciledAtUtc IS NOT NULL),

    CONSTRAINT CK_BP_Variance CHECK ((ApprovedExtractVariancePages = 0 AND ApprovedClassifyVariancePages = 0)

                                  OR (VarianceReason IS NOT NULL AND VarianceApprovedBy IS NOT NULL))

);

GO



-- -----------------------------------------------------------------------------------------------------

-- MonthlyConsumerCosts: the frozen result of closing a month, in whole cents. One row per consumer per month.

-- -----------------------------------------------------------------------------------------------------

CREATE TABLE dbo.MonthlyConsumerCosts (

    BillingPeriod      char(7)        NOT NULL CONSTRAINT FK_MCC_Period REFERENCES dbo.BillingPeriods (BillingPeriod),

    ConsumerId         smallint       NOT NULL CONSTRAINT FK_MCC_Consumers REFERENCES dbo.Consumers (ConsumerId),

    Jobs               int            NOT NULL,

    ExtractPages       int            NOT NULL,

    ClassifyPages      int            NOT NULL,

    PageShare          decimal(9,8)   NOT NULL,   -- ExtractPages / all consumers' ExtractPages

    DiExtractCostUsd   decimal(12,2)  NOT NULL,   -- share of the actual extraction bill, largest remainder

    DiClassifyCostUsd  decimal(12,2)  NOT NULL,   -- share of the actual classification bill

    SharedCostUsd      decimal(12,2)  NOT NULL,   -- share of infra + model development

    TotalCostUsd       AS (DiExtractCostUsd + DiClassifyCostUsd + SharedCostUsd) PERSISTED,

    CONSTRAINT PK_MonthlyConsumerCosts PRIMARY KEY (BillingPeriod, ConsumerId)

);

GO


-- Stage 1 page allowance: same definitions as migration 06.
ALTER TABLE dbo.Consumers ADD
    QuotaMode varchar(10) NOT NULL CONSTRAINT DF_Consumers_QuotaMode DEFAULT ('Unlimited'),
    QuotaWarningPercent tinyint NOT NULL CONSTRAINT DF_Consumers_QuotaWarning DEFAULT (10);
GO
ALTER TABLE dbo.Consumers ADD CONSTRAINT CK_Consumers_QuotaMode CHECK (QuotaMode IN ('Unlimited','Prepaid')),
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
    CONSTRAINT CK_Allowance_Kind CHECK ((Kind='TopUp' AND Pages>0 AND OriginalGrantId IS NULL)
        OR Kind='Adjustment' OR (Kind='Reversal' AND OriginalGrantId IS NOT NULL)),
    CONSTRAINT CK_Allowance_Audit CHECK (LEN(OperatorIdentity)>0 AND LEN(Note)>0)
);
CREATE UNIQUE INDEX UX_Allowance_Reversal ON dbo.PageAllowanceGrants(OriginalGrantId) WHERE Kind='Reversal';
CREATE INDEX IX_Allowance_Consumer ON dbo.PageAllowanceGrants(ConsumerId,RecordedAtUtc,GrantId) INCLUDE(Pages,Kind,BalanceAfter);
CREATE TABLE dbo.PageReservations (
    JobId uniqueidentifier NOT NULL,
    RunNo smallint NOT NULL,
    ConsumerId smallint NOT NULL,
    Pages bigint NOT NULL,
    State varchar(10) NOT NULL CONSTRAINT DF_PageReservations_State DEFAULT ('Reserved'),
    ReservedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_PageReservations_Reserved DEFAULT SYSUTCDATETIME(),
    SettledAtUtc datetime2(3) NULL,
    OperationId bigint NULL CONSTRAINT FK_Reservation_Operation REFERENCES dbo.AzureOperations(OperationId),
    CONSTRAINT PK_PageReservations PRIMARY KEY(JobId,RunNo),
    CONSTRAINT UQ_Reservation_Consumer UNIQUE(JobId,RunNo,ConsumerId),
    CONSTRAINT FK_Reservation_Run FOREIGN KEY(JobId,RunNo,ConsumerId) REFERENCES dbo.JobRuns(JobId,RunNo,ConsumerId),
    CONSTRAINT CK_Reservation_Pages CHECK(Pages>0),
    CONSTRAINT CK_Reservation_State CHECK
        ((State='Reserved' AND SettledAtUtc IS NULL AND OperationId IS NULL)
        OR (State='Charged' AND SettledAtUtc IS NOT NULL AND OperationId IS NOT NULL)
        OR (State='Released' AND SettledAtUtc IS NOT NULL AND OperationId IS NULL))
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
CREATE INDEX IX_PageCharges_Consumer ON dbo.CustomerPageCharges(ConsumerId,RecordedAtUtc,ChargeId) INCLUDE(Pages,JobId,RunNo);

GO
CREATE OR ALTER VIEW dbo.ConsumerPageBalance AS
SELECT c.ConsumerId,
    COALESCE(g.Pages,CONVERT(bigint,0))-COALESCE(ch.Pages,CONVERT(bigint,0))-COALESCE(r.Pages,CONVERT(bigint,0)) AS AvailablePages
FROM dbo.Consumers c
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.PageAllowanceGrants WHERE ConsumerId=c.ConsumerId) g
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.CustomerPageCharges WHERE ConsumerId=c.ConsumerId) ch
OUTER APPLY (SELECT SUM(Pages) Pages FROM dbo.PageReservations WHERE ConsumerId=c.ConsumerId AND State='Reserved') r;
GO
CREATE OR ALTER PROCEDURE dbo.AcquirePageQuotaLock @ConsumerId smallint AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 50016, 'Page quota lock requires a transaction.', 1;
    DECLARE @resource nvarchar(255)=CONCAT('DocAi.PageQuota.',@ConsumerId), @rc int;
    EXEC @rc=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @rc<0 THROW 50016, 'Page quota lock could not be acquired.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Allowance_Insert ON dbo.PageAllowanceGrants INSTEAD OF INSERT AS
BEGIN
    SET NOCOUNT ON;
    -- One audit request at a time; the lock precedes every accounting read/write.
    IF (SELECT COUNT(*) FROM inserted)<>1 THROW 50017, 'One allowance request is required.', 1;
    DECLARE @consumerId smallint=(SELECT ConsumerId FROM inserted);
    EXEC dbo.AcquirePageQuotaLock @consumerId;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.RequestId=i.RequestId)
    BEGIN
        IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.RequestId=i.RequestId
            WHERE g.ConsumerId<>i.ConsumerId OR g.Pages<>i.Pages OR g.Kind<>i.Kind
            OR ISNULL(g.OriginalGrantId,0)<>ISNULL(i.OriginalGrantId,0)
            OR g.OperatorIdentity<>i.OperatorIdentity OR g.Note<>i.Note)
            THROW 50017, 'Allowance request identity conflict.', 1;
        RETURN;
    END;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants g ON g.GrantId=i.OriginalGrantId
              WHERE i.Kind='Reversal' AND (i.Pages<>-g.Pages OR i.ConsumerId<>g.ConsumerId OR g.Kind<>'TopUp'))
        THROW 50017, 'Invalid allowance reversal.', 1;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageAllowanceGrants r ON r.OriginalGrantId=i.OriginalGrantId
              WHERE i.Kind='Reversal' AND r.Kind='Reversal')
        THROW 50017, 'TopUp already reversed.', 1;
    INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId,BalanceAfter)
    SELECT i.ConsumerId,i.Pages,i.Kind,i.OriginalGrantId,i.OperatorIdentity,i.Note,i.RequestId,b.AvailablePages+i.Pages
    FROM inserted i JOIN dbo.ConsumerPageBalance b ON b.ConsumerId=i.ConsumerId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SettlePageReservation @JobId uniqueidentifier,@RunNo smallint AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @consumerId smallint, @status varchar(12), @pages bigint, @usageId bigint, @operationId bigint;
    SELECT @consumerId=ConsumerId FROM dbo.JobRuns WHERE JobId=@JobId AND RunNo=@RunNo;
    DECLARE @resource nvarchar(255)=CONCAT('DocAi.PageQuota.',@consumerId);
    IF @@TRANCOUNT=0 OR APPLOCK_MODE('public',@resource,'Transaction')<>'Exclusive'
        THROW 50016, 'Consumer quota lock must precede settlement.', 1;
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
        UPDATE dbo.PageReservations SET State='Charged',SettledAtUtc=SYSUTCDATETIME(),OperationId=@operationId
        WHERE JobId=@JobId AND RunNo=@RunNo AND State<>'Charged';
    END
    ELSE IF @status<>'Active'
    BEGIN
        UPDATE dbo.PageReservations SET State='Released',SettledAtUtc=SYSUTCDATETIME()
        WHERE JobId=@JobId AND RunNo=@RunNo AND State='Reserved';
        -- Fixed operational signal, no document/customer values. C# maps this to an error log event.
        IF @status='Completed' AND @@ROWCOUNT>0
            RAISERROR('page_quota.completed_without_billed_usage',10,1);
    END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ReconcilePageReservations AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @consumerId smallint,@jobId uniqueidentifier,@runNo smallint;
    DECLARE consumers CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT ConsumerId FROM dbo.PageReservations WHERE State='Reserved';
    OPEN consumers;
    FETCH NEXT FROM consumers INTO @consumerId;
    WHILE @@FETCH_STATUS=0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            EXEC dbo.AcquirePageQuotaLock @consumerId;
            DECLARE runs CURSOR LOCAL STATIC READ_ONLY FOR
                SELECT r.JobId,r.RunNo FROM dbo.JobRuns r JOIN dbo.PageReservations p ON p.JobId=r.JobId AND p.RunNo=r.RunNo
                WHERE p.ConsumerId=@consumerId AND p.State='Reserved' AND
                (r.Status<>'Active' OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.JobId=r.JobId AND l.RunNo=r.RunNo AND l.ReversesUsageId IS NULL));
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
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants INSTEAD OF UPDATE, DELETE AS BEGIN THROW 50017, 'Page accounting is append only.', 1; END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PageCharges_AppendOnly ON dbo.CustomerPageCharges INSTEAD OF UPDATE, DELETE AS BEGIN THROW 50017, 'Page accounting is append only.', 1; END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Reservation_Integrity ON dbo.PageReservations AFTER INSERT,UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.JobRuns r ON r.JobId=i.JobId AND r.RunNo=i.RunNo
              WHERE i.ConsumerId<>r.ConsumerId OR i.Pages<>r.PageCount)
        THROW 50017, 'Reservation must match its run.', 1;
    IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.JobId=d.JobId AND i.RunNo=d.RunNo
        WHERE i.ConsumerId<>d.ConsumerId OR i.Pages<>d.Pages OR i.ReservedAtUtc<>d.ReservedAtUtc
           OR (d.State<>'Reserved' AND (i.State<>d.State OR ISNULL(i.OperationId,0)<>ISNULL(d.OperationId,0)
                                      OR i.SettledAtUtc<>d.SettledAtUtc)))
        THROW 50017, 'Settled reservations are immutable.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PageCharge_Integrity ON dbo.CustomerPageCharges AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PageReservations r ON r.JobId=i.JobId AND r.RunNo=i.RunNo
        JOIN dbo.UsageLedger l ON l.UsageId=i.UsageId JOIN dbo.AzureOperations o ON o.OperationId=i.OperationId
        WHERE i.Pages<>r.Pages OR i.ConsumerId<>r.ConsumerId OR l.ConsumerId<>i.ConsumerId OR l.JobId<>i.JobId
            OR l.RunNo<>i.RunNo OR l.OperationId<>i.OperationId OR l.ReversesUsageId IS NOT NULL
            OR o.JobId<>i.JobId OR o.RunNo<>i.RunNo OR o.State<>'Succeeded')
        THROW 50017, 'Customer charge must match billed run usage.', 1;
END;
GO

-- Stage 4: durable replay identity for mode/warning changes as well as grants.
IF OBJECT_ID('dbo.PageQuotaOperatorRequests','U') IS NULL
CREATE TABLE dbo.PageQuotaOperatorRequests(
 RequestId uniqueidentifier NOT NULL PRIMARY KEY,
 ConsumerId smallint NOT NULL REFERENCES dbo.Consumers(ConsumerId),
 Payload nvarchar(1200) NOT NULL,
 RecordedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);

