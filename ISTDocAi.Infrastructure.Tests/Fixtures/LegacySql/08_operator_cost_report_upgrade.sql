-- Additive and rerunnable. No accounting data or prices are changed.
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF DATABASE_PRINCIPAL_ID('docai_quota_operator') IS NULL THROW 50001,'Apply quota upgrades first.',1;
-- IST cost reporting: column-level read only; no new write permissions.
GRANT SELECT (ConsumerId,JobId,RunNo,MeterCode,Pages,PricePer1000,AzureCostUsd) ON dbo.UsageLedger TO docai_quota_operator;
GRANT SELECT (MeterCode,EffectiveFrom,PricePer1000,Source) ON dbo.AzureMeterPrices TO docai_quota_operator;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
