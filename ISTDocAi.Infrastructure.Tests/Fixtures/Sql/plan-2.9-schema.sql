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
    CONSTRAINT CK_Consumers_Limits   CHECK (MaxPagesPerJob BETWEEN 1 AND 50 AND DailyPageCap > 0)
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
    ShippingLineCode    varchar(20)   NOT NULL,
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
    JobId            uniqueidentifier NOT NULL CONSTRAINT FK_Runs_Jobs REFERENCES dbo.Jobs (JobId),
    RunNo            smallint         NOT NULL,
    Reason           varchar(10)      NOT NULL,   -- New | Requeue | Reprocess
    ConsumerId       smallint         NOT NULL CONSTRAINT FK_Runs_Consumers REFERENCES dbo.Consumers (ConsumerId),
    PageCount        smallint         NOT NULL,   -- counted by the daily page cap ([S1])
    ClientRequestId  varchar(100)     NULL,       -- the submission that started the run
    CorrelationId    varchar(64)      NOT NULL,
    Status           varchar(12)      NOT NULL CONSTRAINT DF_Runs_Status DEFAULT ('Active'),   -- Active | Completed | Failed
    ErrorCode        varchar(50)      NULL,
    ErrorMessage     nvarchar(400)    NULL,
    CreatedAtUtc     datetime2(3)     NOT NULL CONSTRAINT DF_Runs_Created DEFAULT (SYSUTCDATETIME()),
    CompletedAtUtc   datetime2(3)     NULL,
    CONSTRAINT PK_JobRuns PRIMARY KEY (JobId, RunNo),
    CONSTRAINT CK_Runs_Reason CHECK (Reason IN ('New', 'Requeue', 'Reprocess')),
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
