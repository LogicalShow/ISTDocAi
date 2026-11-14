namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class BillingPeriod
{
    public string Period { get; set; } = null!;
    public decimal DiExtractBillUsd { get; set; }
    public decimal DiClassifyBillUsd { get; set; }
    public int AzureExtractPages { get; set; }
    public int AzureClassifyPages { get; set; }
    public decimal InfraCostUsd { get; set; }
    public decimal ModelDevCostUsd { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public string? ReconciledBy { get; set; }
    public int ApprovedExtractVariancePages { get; set; }
    public int ApprovedClassifyVariancePages { get; set; }
    public string? VarianceReason { get; set; }
    public string? VarianceApprovedBy { get; set; }
    public decimal UnallocatedUsd { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
}
