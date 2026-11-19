-- Read only. Dynamic SELECT avoids binding a table absent on older servers.
DECLARE @history nvarchar(max)=N'[]', @available bit=0, @error bit=0;
IF OBJECT_ID('dbo.AzurePriceChecks') IS NOT NULL
BEGIN
 BEGIN TRY
  -- Bounded read: the latest 1000 rows (about 500 checks / 250 days) plus each meter's latest Updated row,
  -- so the menu banner stays fast as the append-only history grows and "Last change" is never lost.
  EXEC sys.sp_executesql N'SELECT @j=(SELECT CheckId checkId,MeterCode meterCode,CheckedAtUtc checkedAtUtc,Outcome outcome,ReasonCode reasonCode,ObservedPrice observedPrice,CurrentPrice currentPrice,ProposedEffectiveFrom proposedEffectiveFrom FROM (
    SELECT CheckId,MeterCode,CheckedAtUtc,Outcome,ReasonCode,ObservedPrice,CurrentPrice,ProposedEffectiveFrom FROM (SELECT TOP (1000) CheckId,MeterCode,CheckedAtUtc,Outcome,ReasonCode,ObservedPrice,CurrentPrice,ProposedEffectiveFrom FROM dbo.AzurePriceChecks ORDER BY CheckedAtUtc DESC,CheckId) recent
    UNION
    SELECT CheckId,MeterCode,CheckedAtUtc,Outcome,ReasonCode,ObservedPrice,CurrentPrice,ProposedEffectiveFrom FROM (SELECT CheckId,MeterCode,CheckedAtUtc,Outcome,ReasonCode,ObservedPrice,CurrentPrice,ProposedEffectiveFrom,ROW_NUMBER() OVER(PARTITION BY MeterCode ORDER BY CheckedAtUtc DESC,CheckId) n FROM dbo.AzurePriceChecks WHERE Outcome=''Updated'') updated WHERE n=1
  ) history ORDER BY CheckedAtUtc,CheckId FOR JSON PATH,INCLUDE_NULL_VALUES)',N'@j nvarchar(max) OUTPUT',@history OUTPUT;
  SET @available=1;
 END TRY
 BEGIN CATCH
  SET @error=1;
 END CATCH
END;
SELECT (SELECT @available historyAvailable,@error historyReadError,JSON_QUERY(@history) checks,
 JSON_QUERY((SELECT a.MeterCode meterCode,a.EffectiveFrom effectiveFrom,a.PricePer1000 pricePer1000,a.Source source FROM dbo.AzureMeterPrices a WHERE a.EffectiveFrom<=CAST(SYSUTCDATETIME() AS date) AND NOT EXISTS (SELECT 1 FROM dbo.AzureMeterPrices b WHERE b.MeterCode=a.MeterCode AND b.EffectiveFrom<=CAST(SYSUTCDATETIME() AS date) AND b.EffectiveFrom>a.EffectiveFrom) ORDER BY a.MeterCode FOR JSON PATH)) currentPrices,
 JSON_QUERY((SELECT MeterCode meterCode,EffectiveFrom effectiveFrom,PricePer1000 pricePer1000,Source source FROM dbo.AzureMeterPrices WHERE EffectiveFrom>CAST(SYSUTCDATETIME() AS date) ORDER BY MeterCode,EffectiveFrom FOR JSON PATH)) scheduledPrices
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AzurePriceStatusJson;
