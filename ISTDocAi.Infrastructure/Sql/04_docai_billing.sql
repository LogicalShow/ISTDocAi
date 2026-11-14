-- =====================================================================================================
-- REFERENCE ONLY - DO NOT RUN THIS FILE AS A SCRIPT. The operator runs these at month end ([B1]-[B9]). Each statement is sent on its own, with
-- its @parameters bound by the code. Generated from the SQL tested on SQL Server 2019 and 2025 with
-- READ_COMMITTED_SNAPSHOT ON (plan revision 2.9).
-- =====================================================================================================

-- [B1] Month to date, any day: a PAYG-EQUIVALENT estimate per consumer, straight from the ledger.
--      Not an upper bound: with the commitment tier the bill can be higher (a 15,000-page month costs
--      $540 on the tier, $450 here). Forecast the tier separately: fixed fee + pages above 20,000 x $0.027.
--      An unpriced row is priced here from the price list by its date, and counted in UnpricedPages.
SELECT c.Code,
       SUM(CASE WHEN l.MeterCode = 'DI_CUSTOM_EXTRACT'  THEN l.Pages ELSE 0 END)     AS ExtractPages,
       SUM(CASE WHEN l.MeterCode = 'DI_CUSTOM_CLASSIFY' THEN l.Pages ELSE 0 END)     AS ClassifyPages,
       SUM(COALESCE(l.AzureCostUsd, l.Pages * p.PricePer1000 / 1000))                AS PaygEquivalentUsd,
       SUM(CASE WHEN l.PricePer1000 IS NULL THEN l.Pages ELSE 0 END)                  AS UnpricedPages
FROM dbo.UsageLedger AS l
JOIN dbo.Consumers   AS c ON c.ConsumerId = l.ConsumerId
OUTER APPLY (SELECT TOP (1) mp.PricePer1000 FROM dbo.AzureMeterPrices AS mp
             WHERE mp.MeterCode = l.MeterCode AND mp.EffectiveFrom <= CAST(l.OccurredAtUtc AS date)
             ORDER BY mp.EffectiveFrom DESC) AS p
WHERE l.BillingPeriod = CONVERT(char(7), SYSUTCDATETIME(), 126)
GROUP BY c.Code
ORDER BY PaygEquivalentUsd DESC;

-- [B2] After the month ends: enter the actual numbers from Cost Management. Any later correction of these
--      numbers must also set ReconciledAtUtc = NULL, so the month is reconciled again before it is closed.
--      Example: the second worked example of section 8 (20,000-page tier, 25,000 pages, growth hosting).
INSERT dbo.BillingPeriods (BillingPeriod, DiExtractBillUsd, DiClassifyBillUsd, AzureExtractPages,
                           AzureClassifyPages, InfraCostUsd, ModelDevCostUsd)
VALUES (@period, @extractBill, @classifyBill, @extractPages, @classifyPages, @infra, @modelDev);

-- [B3] Reconciliation, both meters: the ledger must explain the Azure meter. A meter passes when the
--      ledger, plus any variance approved with [B3c], is within 2 % of Azure; zero on both sides passes; ledger
--      pages against zero Azure pages fail.
SELECT v.MeterCode, l.LedgerPages, v.ApprovedPages, v.AzurePages,
       v.AzurePages - l.LedgerPages                                      AS UnattributedPages,
       CASE WHEN v.AzurePages = 0 AND l.LedgerPages + v.ApprovedPages = 0 THEN CONVERT(decimal(7,2), 0)
            WHEN v.AzurePages = 0 THEN NULL                              -- ledger without Azure usage: mismatch
            ELSE CONVERT(decimal(7,2), 100.0 * (v.AzurePages - l.LedgerPages - v.ApprovedPages) / v.AzurePages)
       END AS VariancePct
FROM dbo.BillingPeriods AS bp
CROSS APPLY (VALUES ('DI_CUSTOM_EXTRACT',  bp.AzureExtractPages,  bp.ApprovedExtractVariancePages),
                    ('DI_CUSTOM_CLASSIFY', bp.AzureClassifyPages, bp.ApprovedClassifyVariancePages)) AS v (MeterCode, AzurePages, ApprovedPages)
CROSS APPLY (SELECT COALESCE(SUM(u.Pages), 0) AS LedgerPages
             FROM dbo.UsageLedger AS u
             WHERE u.BillingPeriod = bp.BillingPeriod AND u.MeterCode = v.MeterCode) AS l
WHERE bp.BillingPeriod = @period;

-- Azure calls that may have been billed without a result (State = 'Unknown'): the usual explanation of a gap.
SELECT o.Kind, COUNT(*) AS UnknownOperations
FROM dbo.AzureOperations AS o
WHERE o.State = 'Unknown' AND CONVERT(char(7), o.SentAtUtc, 126) = @period
GROUP BY o.Kind;

-- [B3b] Record that the month passed reconciliation — the gate [B4] checks. Fails unless BOTH meters pass.
UPDATE bp
SET ReconciledAtUtc = SYSUTCDATETIME(), ReconciledBy = @reconciledBy
FROM dbo.BillingPeriods AS bp
WHERE bp.BillingPeriod = @period AND bp.Status = 'Open'
  AND NOT EXISTS (
        SELECT 1
        FROM (VALUES ('DI_CUSTOM_EXTRACT',  bp.AzureExtractPages,  bp.ApprovedExtractVariancePages),
                     ('DI_CUSTOM_CLASSIFY', bp.AzureClassifyPages, bp.ApprovedClassifyVariancePages)) AS v (MeterCode, AzurePages, ApprovedPages)
        CROSS APPLY (SELECT COALESCE(SUM(u.Pages), 0) AS LedgerPages
                     FROM dbo.UsageLedger AS u
                     WHERE u.BillingPeriod = bp.BillingPeriod AND u.MeterCode = v.MeterCode) AS l
        WHERE (v.AzurePages = 0 AND l.LedgerPages + v.ApprovedPages <> 0)
           OR ABS(v.AzurePages - l.LedgerPages - v.ApprovedPages) * 100.0 / NULLIF(v.AzurePages, 0) > 2);
           -- NULLIF: SQL may evaluate either side first
IF @@ROWCOUNT <> 1 THROW 50005, 'Reconciliation failed: a meter is outside 2 %, or the period is not open.', 1;

-- [B3c] Approve a variance, after the investigation of month-end step 3: pages Azure billed that the ledger
--       can't attribute — typically an Unknown call that Azure did bill, so the ledger has no row for it — per
--       meter, with the reason and the approver. [B3b] and [B4] then compare the ledger plus these pages with
--       Azure. Their cost needs no ledger row: [B4] allocates the ACTUAL bill by page share, so unattributed
--       pages are spread the same way as every other cost. Clears the gate: run [B3b] again.
UPDATE dbo.BillingPeriods
SET ApprovedExtractVariancePages = @extractPages, ApprovedClassifyVariancePages = @classifyPages,
    VarianceReason = @reason, VarianceApprovedBy = @approvedBy,
    ReconciledAtUtc = NULL, ReconciledBy = NULL
WHERE BillingPeriod = @period AND Status = 'Open';
IF @@ROWCOUNT <> 1 THROW 50007, 'The period does not exist or is already closed.', 1;

-- [B4] Close the month: allocate the ACTUAL bill in whole cents. Each consumer gets the whole cents of its
--      exact share; the leftover cents go to the largest fractions (largest remainder), so the rows add up
--      to the bill exactly. A meter nobody used goes to UnallocatedUsd for the operator to book.
--      Refused unless [B3b] passed AND both meters still pass now, on the ledger rows this transaction locks:
--      a row that arrived after [B3b] can't slip past the check, and a row being written for the month waits
--      until the close commits. A closed month can't be closed twice. Anything recorded for it later — or
--      written while the close ran — keeps its month and is reported by [B8], which compares totals.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.BillingPeriods WITH (UPDLOCK, HOLDLOCK)
               WHERE BillingPeriod = @period AND Status = 'Open' AND ReconciledAtUtc IS NOT NULL)
    THROW 50002, 'The period does not exist, is already closed, or has not passed reconciliation.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.BillingPeriods AS bp
    CROSS APPLY (VALUES ('DI_CUSTOM_EXTRACT',  bp.AzureExtractPages,  bp.ApprovedExtractVariancePages),
                        ('DI_CUSTOM_CLASSIFY', bp.AzureClassifyPages, bp.ApprovedClassifyVariancePages)) AS v (MeterCode, AzurePages, ApprovedPages)
    CROSS APPLY (SELECT COALESCE(SUM(u.Pages), 0) AS LedgerPages
                 FROM dbo.UsageLedger AS u WITH (HOLDLOCK)
                 WHERE u.BillingPeriod = bp.BillingPeriod AND u.MeterCode = v.MeterCode) AS l
    WHERE bp.BillingPeriod = @period
      AND (   (v.AzurePages = 0 AND l.LedgerPages + v.ApprovedPages <> 0)
           OR ABS(v.AzurePages - l.LedgerPages - v.ApprovedPages) * 100.0 / NULLIF(v.AzurePages, 0) > 2))
    THROW 50005, 'The ledger changed since reconciliation: run [B3] and [B3b] again.', 1;

WITH month_usage AS (
    SELECT ConsumerId,
           COUNT(DISTINCT JobId)                                                    AS Jobs,
           SUM(CASE WHEN MeterCode = 'DI_CUSTOM_EXTRACT'  THEN Pages ELSE 0 END)    AS ExtractPages,
           SUM(CASE WHEN MeterCode = 'DI_CUSTOM_CLASSIFY' THEN Pages ELSE 0 END)    AS ClassifyPages
    FROM dbo.UsageLedger
    WHERE BillingPeriod = @period
    GROUP BY ConsumerId
), totals AS (
    SELECT SUM(ExtractPages) AS AllExtract, SUM(ClassifyPages) AS AllClassify FROM month_usage
), bill AS (
    SELECT CONVERT(bigint, ROUND(DiExtractBillUsd * 100, 0))                   AS ExtractCents,
           CONVERT(bigint, ROUND(DiClassifyBillUsd * 100, 0))                  AS ClassifyCents,
           CONVERT(bigint, ROUND((InfraCostUsd + ModelDevCostUsd) * 100, 0))   AS SharedCents
    FROM dbo.BillingPeriods WHERE BillingPeriod = @period
), exact AS (          -- each consumer's exact share, in cents. decimal(19,6) on purpose: left at the
                       -- default precision (38), "x - FLOOR(x)" loses its scale and every fraction becomes 1
    SELECT u.ConsumerId, u.Jobs, u.ExtractPages, u.ClassifyPages, t.AllExtract, t.AllClassify,
           b.ExtractCents, b.ClassifyCents, b.SharedCents,
           CONVERT(decimal(19,8), COALESCE(1.0 * u.ExtractPages / NULLIF(t.AllExtract, 0), 0))                     AS PageShare,
           CONVERT(decimal(19,6), COALESCE(1.0 * b.ExtractCents  * u.ExtractPages  / NULLIF(t.AllExtract, 0), 0))  AS ExtractExact,
           CONVERT(decimal(19,6), COALESCE(1.0 * b.ClassifyCents * u.ClassifyPages / NULLIF(t.AllClassify, 0), 0)) AS ClassifyExact,
           CONVERT(decimal(19,6), COALESCE(1.0 * b.SharedCents   * u.ExtractPages  / NULLIF(t.AllExtract, 0), 0))  AS SharedExact
    FROM month_usage AS u CROSS JOIN totals AS t CROSS JOIN bill AS b
), ranked AS (         -- largest remainder: whole cents first, leftover cents to the largest fractions
    SELECT e.*,
           FLOOR(ExtractExact) AS ExtractFloor, FLOOR(ClassifyExact) AS ClassifyFloor, FLOOR(SharedExact) AS SharedFloor,
           ROW_NUMBER() OVER (ORDER BY ExtractExact  - FLOOR(ExtractExact)  DESC, ConsumerId) AS ExtractRank,
           ROW_NUMBER() OVER (ORDER BY ClassifyExact - FLOOR(ClassifyExact) DESC, ConsumerId) AS ClassifyRank,
           ROW_NUMBER() OVER (ORDER BY SharedExact   - FLOOR(SharedExact)   DESC, ConsumerId) AS SharedRank,
           SUM(FLOOR(ExtractExact))  OVER () AS ExtractFloorSum,
           SUM(FLOOR(ClassifyExact)) OVER () AS ClassifyFloorSum,
           SUM(FLOOR(SharedExact))   OVER () AS SharedFloorSum
    FROM exact AS e
)
INSERT dbo.MonthlyConsumerCosts (BillingPeriod, ConsumerId, Jobs, ExtractPages, ClassifyPages, PageShare,
                                 DiExtractCostUsd, DiClassifyCostUsd, SharedCostUsd)
SELECT @period, ConsumerId, Jobs, ExtractPages, ClassifyPages, CONVERT(decimal(9,8), PageShare),
       (ExtractFloor  + CASE WHEN AllExtract  > 0 AND ExtractRank  <= ExtractCents  - ExtractFloorSum  THEN 1 ELSE 0 END) / 100.0,
       (ClassifyFloor + CASE WHEN AllClassify > 0 AND ClassifyRank <= ClassifyCents - ClassifyFloorSum THEN 1 ELSE 0 END) / 100.0,
       (SharedFloor   + CASE WHEN AllExtract  > 0 AND SharedRank   <= SharedCents   - SharedFloorSum   THEN 1 ELSE 0 END) / 100.0
FROM ranked;

UPDATE bp
SET Status = 'Closed', ClosedAtUtc = SYSUTCDATETIME(), ClosedBy = @closedBy,
    UnallocatedUsd = bp.DiExtractBillUsd + bp.DiClassifyBillUsd + bp.InfraCostUsd + bp.ModelDevCostUsd
                   - COALESCE((SELECT SUM(m.TotalCostUsd) FROM dbo.MonthlyConsumerCosts AS m
                               WHERE m.BillingPeriod = @period), 0)
FROM dbo.BillingPeriods AS bp
WHERE bp.BillingPeriod = @period AND bp.Status = 'Open';

IF EXISTS (SELECT 1 FROM dbo.BillingPeriods WHERE BillingPeriod = @period AND UnallocatedUsd < 0)
    THROW 50004, 'More than the bill was allocated.', 1;

COMMIT;

-- [B5] Statement: per consumer, subtotal per company (tenant), grand total, and the unallocated line if any.
SELECT CASE WHEN GROUPING(c.TenantCode) = 1 THEN 'ALL'
            WHEN GROUPING(c.Code) = 1       THEN c.TenantCode + ' total'
            ELSE c.Code END                                                AS Line,
       SUM(m.ExtractPages)                                                 AS ExtractPages,
       SUM(m.ClassifyPages)                                                AS ClassifyPages,
       SUM(m.DiExtractCostUsd + m.DiClassifyCostUsd)                       AS DocumentIntelligenceUsd,
       SUM(m.SharedCostUsd)                                                AS SharedUsd,
       SUM(m.TotalCostUsd)                                                 AS TotalUsd,
       CONVERT(decimal(9,4), SUM(m.TotalCostUsd) / NULLIF(SUM(m.ExtractPages), 0)) AS UsdPerPage
FROM dbo.MonthlyConsumerCosts AS m
JOIN dbo.Consumers            AS c ON c.ConsumerId = m.ConsumerId
WHERE m.BillingPeriod = @period
GROUP BY ROLLUP (c.TenantCode, c.Code)
ORDER BY GROUPING(c.TenantCode), c.TenantCode, GROUPING(c.Code), c.Code;

SELECT UnallocatedUsd FROM dbo.BillingPeriods WHERE BillingPeriod = @period AND UnallocatedUsd <> 0;

-- [B6] Per product instead of per company: the same query with ROLLUP (c.ProductCode, c.Code).

-- [B7] Correction (rare): reverse one ledger row with a negative row IN THE ORIGINAL ROW'S MONTH — the month
--      Azure did the work in, like every other row (RecordedAtUtc says when it was booked); the original is
--      never edited. If that month is still open, its reconciliation is cleared so it is run again. If it is
--      closed, the reversal is a late adjustment: [B8] reports it, nothing is re-allocated, and no other
--      month's reconciliation moves. UX_Usage_OneReversal makes a second reversal of the same row fail
--      (treat as "already done").
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @period char(7) = (SELECT BillingPeriod FROM dbo.UsageLedger
                           WHERE UsageId = @usageId AND ReversesUsageId IS NULL);
IF @period IS NULL THROW 50006, 'No original ledger row with that id.', 1;

-- The period row first, then the ledger: the same lock order as [B4], so a reversal and a close never deadlock.
DECLARE @periodRows int = (SELECT COUNT(*) FROM dbo.BillingPeriods WITH (UPDLOCK, HOLDLOCK)
                           WHERE BillingPeriod = @period);

INSERT dbo.UsageLedger (ConsumerId, JobId, RunNo, OperationId, MeterCode, ModelId, Pages, PricePer1000,
                        OccurredAtUtc, ReversesUsageId, CorrelationId)
SELECT ConsumerId, JobId, RunNo, OperationId, MeterCode, ModelId, -Pages, PricePer1000,
       OccurredAtUtc, UsageId, @correlationId
FROM dbo.UsageLedger
WHERE UsageId = @usageId AND ReversesUsageId IS NULL;

UPDATE dbo.BillingPeriods SET ReconciledAtUtc = NULL, ReconciledBy = NULL
WHERE BillingPeriod = @period AND Status = 'Open';

COMMIT;

-- [B8] Late usage: what the ledger holds for a closed month beyond what its close allocated — work recorded
--      after the close (or while it ran) and reversals booked later. It compares totals with
--      MonthlyConsumerCosts, so no timing can hide a row. Azure billed that work in its own month, so its cost
--      was already inside that month's bill and was spread by page share: it is listed for audit, never
--      allocated again, and no other month's reconciliation moves. With month-end step 1 (close on the 3rd
--      day or later) it stays empty in practice: Azure keeps a result 24 hours, so work is recorded within a day.
WITH ledger AS (
    SELECT l.BillingPeriod, l.ConsumerId,
           SUM(CASE WHEN l.MeterCode = 'DI_CUSTOM_EXTRACT'  THEN l.Pages ELSE 0 END) AS ExtractPages,
           SUM(CASE WHEN l.MeterCode = 'DI_CUSTOM_CLASSIFY' THEN l.Pages ELSE 0 END) AS ClassifyPages
    FROM dbo.UsageLedger    AS l
    JOIN dbo.BillingPeriods AS bp ON bp.BillingPeriod = l.BillingPeriod AND bp.Status = 'Closed'
    GROUP BY l.BillingPeriod, l.ConsumerId
)
SELECT g.BillingPeriod, c.Code,
       g.ExtractPages  - COALESCE(m.ExtractPages, 0)  AS LateExtractPages,
       g.ClassifyPages - COALESCE(m.ClassifyPages, 0) AS LateClassifyPages
FROM ledger AS g
JOIN dbo.Consumers AS c ON c.ConsumerId = g.ConsumerId
LEFT JOIN dbo.MonthlyConsumerCosts AS m ON m.BillingPeriod = g.BillingPeriod AND m.ConsumerId = g.ConsumerId
WHERE g.ExtractPages <> COALESCE(m.ExtractPages, 0) OR g.ClassifyPages <> COALESCE(m.ClassifyPages, 0)
ORDER BY g.BillingPeriod, c.Code;

-- [B9] The "unpriced usage" alert: rows recorded without a price for which the price list STILL has no price in
--      force at the row's own date. Adding the missing AzureMeterPrices row clears the alert; the ledger row
--      itself never changes ([B1] prices it from the list).
SELECT l.MeterCode, MIN(l.OccurredAtUtc) AS FirstUsageAtUtc, COUNT(*) AS UnpricedRows, SUM(l.Pages) AS UnpricedPages
FROM dbo.UsageLedger AS l
WHERE l.PricePer1000 IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.AzureMeterPrices AS p
                  WHERE p.MeterCode = l.MeterCode AND p.EffectiveFrom <= CAST(l.OccurredAtUtc AS date))
GROUP BY l.MeterCode;
