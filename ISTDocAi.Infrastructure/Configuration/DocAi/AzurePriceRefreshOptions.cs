namespace ISTDocAi.Infrastructure.Configuration.DocAi;
public sealed class AzurePriceRefreshOptions
{
    public bool Enabled { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);
}
