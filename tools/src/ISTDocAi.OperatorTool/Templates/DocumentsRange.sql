DECLARE @consumerId smallint=(SELECT ConsumerId FROM dbo.Consumers WHERE Code=N'$consumer');
IF @consumerId IS NULL THROW 50018, 'Consumer not found.', 1;
DECLARE @fromUtc datetime2(7)=CONVERT(datetime2(7),'$fromUtc',126), @toExclusiveUtc datetime2(7)=CONVERT(datetime2(7),'$toExclusiveUtc',126);
SELECT c.ChargeId AS chargeId,c.JobId AS jobId,c.RunNo AS runNo,c.Pages AS pages,c.RecordedAtUtc AS recordedAtUtc,
       r.ResolvedDocumentTypeCode AS resolvedDocumentTypeCode,r.Reason AS runReason
FROM dbo.CustomerPageCharges c
JOIN dbo.JobRuns r ON r.JobId=c.JobId AND r.RunNo=c.RunNo AND r.ConsumerId=c.ConsumerId
WHERE c.ConsumerId=@consumerId AND c.RecordedAtUtc>=@fromUtc AND c.RecordedAtUtc<@toExclusiveUtc
ORDER BY c.RecordedAtUtc,c.ChargeId;
