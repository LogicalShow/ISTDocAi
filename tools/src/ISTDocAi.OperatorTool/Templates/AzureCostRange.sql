DECLARE @consumerId smallint=NULL;
IF N'$consumer'<>N'' BEGIN
 SET @consumerId=(SELECT ConsumerId FROM dbo.Consumers WHERE Code=N'$consumer');
 IF @consumerId IS NULL THROW 50018, 'Consumer not found.', 1;
END;
DECLARE @fromUtc datetime2(7)=CONVERT(datetime2(7),'$fromUtc',126), @toExclusiveUtc datetime2(7)=CONVERT(datetime2(7),'$toExclusiveUtc',126);
;WITH Charges AS (
 SELECT ConsumerId,JobId,RunNo,SUM(CONVERT(bigint,Pages)) pages FROM dbo.CustomerPageCharges
 WHERE RecordedAtUtc>=@fromUtc AND RecordedAtUtc<@toExclusiveUtc AND (@consumerId IS NULL OR ConsumerId=@consumerId)
 GROUP BY ConsumerId,JobId,RunNo
), Included AS (
 SELECT l.*,CASE WHEN c.ConsumerId IS NULL THEN 0 ELSE 1 END charged
 FROM dbo.UsageLedger l LEFT JOIN Charges c ON c.ConsumerId=l.ConsumerId AND c.JobId=l.JobId AND c.RunNo=l.RunNo
 WHERE (@consumerId IS NULL OR l.ConsumerId=@consumerId) AND (c.ConsumerId IS NOT NULL OR
 (l.OccurredAtUtc>=@fromUtc AND l.OccurredAtUtc<@toExclusiveUtc AND NOT EXISTS
 (SELECT 1 FROM dbo.CustomerPageCharges a WHERE a.ConsumerId=l.ConsumerId AND a.JobId=l.JobId AND a.RunNo=l.RunNo)))
), Priced AS (
 SELECT l.*,p.EffectiveFrom,p.Source,COALESCE(l.PricePer1000,p.PricePer1000) usedPrice,
 COALESCE(l.AzureCostUsd,0) frozen,
 CASE WHEN l.AzureCostUsd IS NULL AND p.PricePer1000 IS NOT NULL THEN CONVERT(decimal(28,10),l.Pages)*p.PricePer1000/1000 ELSE 0 END recovered,
 CASE WHEN l.AzureCostUsd IS NULL AND p.PricePer1000 IS NULL THEN CONVERT(bigint,l.Pages) ELSE CONVERT(bigint,0) END unresolved
 FROM Included l OUTER APPLY (SELECT TOP(1) a.* FROM dbo.AzureMeterPrices a
 WHERE a.MeterCode=l.MeterCode AND a.EffectiveFrom<=CAST(l.OccurredAtUtc AS date)
 AND (l.PricePer1000 IS NULL OR a.PricePer1000=l.PricePer1000) ORDER BY a.EffectiveFrom DESC) p
), Population AS (
 SELECT ConsumerId,COUNT_BIG(*) chargedRuns,SUM(pages) chargedPages FROM Charges GROUP BY ConsumerId
), Costs AS (
 SELECT ConsumerId,
SUM(CASE WHEN charged=1 THEN CASE WHEN MeterCode='DI_CUSTOM_CLASSIFY' THEN CONVERT(bigint,Pages) ELSE CONVERT(bigint,0) END ELSE 0 END) chargedAzureClassifyPages,
SUM(CASE WHEN charged=1 THEN CASE WHEN MeterCode='DI_CUSTOM_EXTRACT' THEN CONVERT(bigint,Pages) ELSE CONVERT(bigint,0) END ELSE 0 END) chargedAzureExtractPages,
SUM(CASE WHEN charged=1 THEN frozen ELSE 0 END) chargedFrozenCostUsd,
SUM(CASE WHEN charged=1 THEN recovered ELSE 0 END) chargedRecoveredEstimateUsd,
SUM(CASE WHEN charged=1 THEN unresolved ELSE 0 END) chargedUnresolvedPages,
SUM(CASE WHEN charged=1 THEN frozen+recovered ELSE 0 END) chargedEstimatedCostUsd,
SUM(CASE WHEN charged=0 THEN CASE WHEN MeterCode='DI_CUSTOM_CLASSIFY' THEN CONVERT(bigint,Pages) ELSE CONVERT(bigint,0) END ELSE 0 END) unchargedAzureClassifyPages,
SUM(CASE WHEN charged=0 THEN CASE WHEN MeterCode='DI_CUSTOM_EXTRACT' THEN CONVERT(bigint,Pages) ELSE CONVERT(bigint,0) END ELSE 0 END) unchargedAzureExtractPages,
SUM(CASE WHEN charged=0 THEN frozen ELSE 0 END) unchargedFrozenCostUsd,
SUM(CASE WHEN charged=0 THEN recovered ELSE 0 END) unchargedRecoveredEstimateUsd,
SUM(CASE WHEN charged=0 THEN unresolved ELSE 0 END) unchargedUnresolvedPages,
SUM(CASE WHEN charged=0 THEN frozen+recovered ELSE 0 END) unchargedEstimatedCostUsd
 FROM Priced GROUP BY ConsumerId
), Combined AS (
 SELECT c.Code consumerCode,c.Name consumerName,
COALESCE(p.chargedRuns,0) chargedRuns,
COALESCE(p.chargedPages,0) chargedPages,
COALESCE(k.chargedAzureClassifyPages,0) chargedAzureClassifyPages,
COALESCE(k.chargedAzureExtractPages,0) chargedAzureExtractPages,
COALESCE(k.chargedFrozenCostUsd,0) chargedFrozenCostUsd,
COALESCE(k.chargedRecoveredEstimateUsd,0) chargedRecoveredEstimateUsd,
COALESCE(k.chargedUnresolvedPages,0) chargedUnresolvedPages,
COALESCE(k.chargedEstimatedCostUsd,0) chargedEstimatedCostUsd,
COALESCE(k.unchargedAzureClassifyPages,0) unchargedAzureClassifyPages,
COALESCE(k.unchargedAzureExtractPages,0) unchargedAzureExtractPages,
COALESCE(k.unchargedFrozenCostUsd,0) unchargedFrozenCostUsd,
COALESCE(k.unchargedRecoveredEstimateUsd,0) unchargedRecoveredEstimateUsd,
COALESCE(k.unchargedEstimatedCostUsd,0) unchargedEstimatedCostUsd,
COALESCE(k.unchargedUnresolvedPages,0) unchargedUnresolvedPages
 FROM dbo.Consumers c LEFT JOIN Population p ON p.ConsumerId=c.ConsumerId LEFT JOIN Costs k ON k.ConsumerId=c.ConsumerId
 WHERE p.ConsumerId IS NOT NULL OR k.ConsumerId IS NOT NULL
), Results AS (
 SELECT *,CONVERT(decimal(28,10),CONVERT(decimal(28,10),chargedEstimatedCostUsd)/NULLIF(CONVERT(decimal(28,10),chargedPages),0)) costPerChargedPageUsd FROM Combined
)
SELECT (SELECT
 JSON_QUERY((SELECT * FROM Results ORDER BY chargedEstimatedCostUsd+unchargedEstimatedCostUsd DESC,consumerCode FOR JSON PATH,INCLUDE_NULL_VALUES)) consumers,
 JSON_QUERY((SELECT
COALESCE(SUM(chargedRuns),0) chargedRuns,
COALESCE(SUM(chargedPages),0) chargedPages,
COALESCE(SUM(chargedAzureClassifyPages),0) chargedAzureClassifyPages,
COALESCE(SUM(chargedAzureExtractPages),0) chargedAzureExtractPages,
COALESCE(SUM(chargedFrozenCostUsd),0) chargedFrozenCostUsd,
COALESCE(SUM(chargedRecoveredEstimateUsd),0) chargedRecoveredEstimateUsd,
COALESCE(SUM(chargedUnresolvedPages),0) chargedUnresolvedPages,
COALESCE(SUM(chargedEstimatedCostUsd),0) chargedEstimatedCostUsd,
COALESCE(SUM(unchargedAzureClassifyPages),0) unchargedAzureClassifyPages,
COALESCE(SUM(unchargedAzureExtractPages),0) unchargedAzureExtractPages,
COALESCE(SUM(unchargedFrozenCostUsd),0) unchargedFrozenCostUsd,
COALESCE(SUM(unchargedRecoveredEstimateUsd),0) unchargedRecoveredEstimateUsd,
COALESCE(SUM(unchargedEstimatedCostUsd),0) unchargedEstimatedCostUsd,
COALESCE(SUM(unchargedUnresolvedPages),0) unchargedUnresolvedPages,
 CONVERT(decimal(28,10),CONVERT(decimal(28,10),SUM(chargedEstimatedCostUsd))/NULLIF(CONVERT(decimal(28,10),SUM(chargedPages)),0)) costPerChargedPageUsd
 FROM Results FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) totals,
 JSON_QUERY((SELECT MeterCode meterCode,usedPrice pricePer1000,EffectiveFrom effectiveFrom,Source source,MIN(OccurredAtUtc) minOccurredAtUtc,MAX(OccurredAtUtc) maxOccurredAtUtc
 FROM Priced WHERE usedPrice IS NOT NULL GROUP BY MeterCode,usedPrice,EffectiveFrom,Source FOR JSON PATH)) pricesUsed,
 JSON_QUERY((SELECT a.MeterCode meterCode,a.PricePer1000 pricePer1000,a.Currency currency,a.EffectiveFrom effectiveFrom,a.Source source
 FROM dbo.AzureMeterPrices a WHERE a.EffectiveFrom<=CAST(SYSUTCDATETIME() AS date) AND NOT EXISTS
 (SELECT 1 FROM dbo.AzureMeterPrices b WHERE b.MeterCode=a.MeterCode AND b.EffectiveFrom<=CAST(SYSUTCDATETIME() AS date) AND b.EffectiveFrom>a.EffectiveFrom) ORDER BY a.MeterCode FOR JSON PATH)) currentPriceList,
 JSON_QUERY((SELECT MeterCode meterCode,PricePer1000 pricePer1000,Currency currency,EffectiveFrom effectiveFrom,Source source FROM dbo.AzureMeterPrices WHERE EffectiveFrom>CAST(SYSUTCDATETIME() AS date) ORDER BY MeterCode,EffectiveFrom FOR JSON PATH)) scheduledPrices,
 N'ESTIMATE from Azure list prices (pay-as-you-go). The Azure invoice is authoritative; commitment tiers can exceed this PAYG-equivalent estimate and discounts can differ.' costNotice
 FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) AzureCostJson;
