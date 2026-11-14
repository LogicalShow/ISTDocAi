-- =====================================================================================================

-- DocAi: roles and permissions. Applied by DbUp after 01_docai_schema.sql, run by the pipeline identity.

-- The users themselves are created by 00_bootstrap_identities.sql (Entra admin, once per environment).

-- =====================================================================================================



-- Runtime: the App Service identity. Reads everything it needs; writes only the processing tables;

-- can only APPEND to the ledger. It cannot change consumers, models, prices or closed months.

CREATE ROLE docai_runtime;

GRANT SELECT ON SCHEMA::dbo                            TO docai_runtime;

GRANT INSERT, UPDATE ON dbo.Jobs                       TO docai_runtime;

GRANT INSERT, UPDATE ON dbo.JobRuns                    TO docai_runtime;   -- UPDATE: the run's outcome

GRANT INSERT, DELETE ON dbo.JobSubmissions             TO docai_runtime;   -- DELETE: retention [S15]

GRANT INSERT, UPDATE ON dbo.AzureOperations            TO docai_runtime;

GRANT INSERT, DELETE ON dbo.JobResults                 TO docai_runtime;   -- DELETE: retention [S15]

GRANT INSERT, UPDATE, DELETE ON dbo.WebhookDeliveries  TO docai_runtime;   -- DELETE: retention [S15]

GRANT INSERT ON dbo.UsageLedger                        TO docai_runtime;   -- append only: no UPDATE, no DELETE

GO



-- Operators (an Entra group): consumers, prices, the month close, reversals and webhook replay.

CREATE ROLE docai_operator;

GRANT SELECT ON SCHEMA::dbo                                TO docai_operator;

GRANT INSERT, UPDATE ON dbo.Consumers                      TO docai_operator;

GRANT INSERT ON dbo.AzureMeterPrices                       TO docai_operator;

GRANT INSERT, UPDATE ON dbo.BillingPeriods                 TO docai_operator;

GRANT INSERT ON dbo.MonthlyConsumerCosts                   TO docai_operator;

GRANT INSERT ON dbo.UsageLedger                            TO docai_operator;   -- reversal rows [B7] only

GRANT UPDATE ON dbo.WebhookDeliveries                      TO docai_operator;   -- replay [S12]

GO



-- Model team (an Entra group): the model registry and the classifier class map.

CREATE ROLE docai_modelteam;

GRANT SELECT ON SCHEMA::dbo                                TO docai_modelteam;

GRANT INSERT, UPDATE ON dbo.ModelRegistry                  TO docai_modelteam;

GRANT INSERT, UPDATE, DELETE ON dbo.ClassifierClasses      TO docai_modelteam;

GO



-- Members. The names are DbUp variables per environment ($AppIdentityName$, ...).

ALTER ROLE docai_runtime   ADD MEMBER [$AppIdentityName$];

ALTER ROLE docai_operator  ADD MEMBER [$OperatorGroupName$];

ALTER ROLE docai_modelteam ADD MEMBER [$ModelTeamGroupName$];

GO


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

GO

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

-- IST cost reporting: column-level read only; no new write permissions.
GRANT SELECT (ConsumerId,JobId,RunNo,MeterCode,Pages,PricePer1000,AzureCostUsd) ON dbo.UsageLedger TO docai_quota_operator;
GRANT SELECT (MeterCode,EffectiveFrom,PricePer1000,Source) ON dbo.AzureMeterPrices TO docai_quota_operator;
