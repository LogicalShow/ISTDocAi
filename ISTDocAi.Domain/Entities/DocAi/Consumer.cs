namespace ISTDocAi.Domain.Entities.DocAi;

/// <summary>Database row only. Runtime persistence remains Dapper.</summary>
public sealed class Consumer
{
    public short ConsumerId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ProductCode { get; set; } = null!;
    public string TenantCode { get; set; } = null!;
    public byte[] ApiKeyHash1 { get; set; } = null!;
    public byte[]? ApiKeyHash2 { get; set; }
    public string? CallbackUrl { get; set; }
    public string? WebhookSecretName { get; set; }
    public string? CallbackCaCertName { get; set; }
    public short MaxPagesPerJob { get; set; }
    public int DailyPageCap { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string QuotaMode { get; set; } = null!;
    public byte QuotaWarningPercent { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
