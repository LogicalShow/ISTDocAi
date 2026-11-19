DECLARE @consumerId smallint=(SELECT ConsumerId FROM dbo.Consumers WHERE Code=N'$consumer');
IF @consumerId IS NULL THROW 50018, 'Consumer not found.', 1;
DECLARE @fromUtc datetime2(7)=CONVERT(datetime2(7),'$fromUtc',126), @toExclusiveUtc datetime2(7)=CONVERT(datetime2(7),'$toExclusiveUtc',126);
;WITH G AS (
 SELECT COALESCE(SUM(CASE WHEN RecordedAtUtc<@fromUtc THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS beforePages,
        COALESCE(SUM(CONVERT(bigint,Pages)),0) AS allPages,
        COALESCE(SUM(CASE WHEN RecordedAtUtc>=@fromUtc AND Kind='TopUp' THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS topUpPages,
        COALESCE(SUM(CASE WHEN RecordedAtUtc>=@fromUtc AND Kind='Adjustment' THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS adjustmentPages,
        COALESCE(SUM(CASE WHEN RecordedAtUtc>=@fromUtc AND Kind='Reversal' THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS reversalPages
 FROM dbo.PageAllowanceGrants WHERE ConsumerId=@consumerId AND RecordedAtUtc<@toExclusiveUtc
), C AS (
 SELECT COALESCE(SUM(CASE WHEN RecordedAtUtc<@fromUtc THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS beforePages,
        COALESCE(SUM(CONVERT(bigint,Pages)),0) AS allPages,
        COALESCE(SUM(CASE WHEN RecordedAtUtc>=@fromUtc THEN CONVERT(bigint,Pages) ELSE 0 END),0) AS chargedPages,
        COUNT_BIG(CASE WHEN RecordedAtUtc>=@fromUtc THEN 1 END) AS chargedRuns
 FROM dbo.CustomerPageCharges WHERE ConsumerId=@consumerId AND RecordedAtUtc<@toExclusiveUtc
)
SELECT (SELECT N'$consumer' AS consumerCode,(SELECT Name FROM dbo.Consumers WHERE ConsumerId=@consumerId) AS consumerName,
 G.beforePages-C.beforePages AS openingBalance,G.topUpPages,G.adjustmentPages,G.reversalPages,C.chargedPages,C.chargedRuns,
 G.beforePages-C.beforePages+G.topUpPages+G.adjustmentPages+G.reversalPages-C.chargedPages AS closingBalance,
 G.allPages-C.allPages AS independentClosingBalance,
 CAST(CASE WHEN (SELECT COUNT_BIG(*) FROM dbo.PageAllowanceGrants WHERE ConsumerId=@consumerId AND RecordedAtUtc>=@fromUtc AND RecordedAtUtc<@toExclusiveUtc)>1000 THEN 1 ELSE 0 END AS bit) AS hasMoreMovements,
 JSON_QUERY((SELECT TOP (1000) g.RecordedAtUtc AS recordedAtUtc,g.Kind AS kind,g.Pages AS pages,
 o.RecordedAtUtc AS originalRecordedAtUtc,o.Pages AS originalPages
 FROM dbo.PageAllowanceGrants g LEFT JOIN dbo.PageAllowanceGrants o ON g.Kind='Reversal' AND o.GrantId=g.OriginalGrantId AND o.ConsumerId=g.ConsumerId
 WHERE g.ConsumerId=@consumerId AND g.RecordedAtUtc>=@fromUtc AND g.RecordedAtUtc<@toExclusiveUtc
 ORDER BY g.RecordedAtUtc,g.GrantId FOR JSON PATH)) AS movements,
 JSON_QUERY((SELECT DATEADD(hour,DATEDIFF(hour,0,RecordedAtUtc),0) AS hourUtc,COUNT_BIG(*) AS runs,SUM(CONVERT(bigint,Pages)) AS pages
 FROM dbo.CustomerPageCharges WHERE ConsumerId=@consumerId AND RecordedAtUtc>=@fromUtc AND RecordedAtUtc<@toExclusiveUtc
 GROUP BY DATEADD(hour,DATEDIFF(hour,0,RecordedAtUtc),0) ORDER BY hourUtc FOR JSON PATH)) AS usageByHour
 FROM G CROSS JOIN C FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS StatementJson;
