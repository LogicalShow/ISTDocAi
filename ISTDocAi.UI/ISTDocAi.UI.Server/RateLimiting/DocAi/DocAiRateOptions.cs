namespace ISTDocAi.UI.Server.RateLimiting.DocAi;
public sealed class DocAiRateOptions
{
    public int TokenLimit { get; set; } = 60;
    public int TokensPerPeriod { get; set; } = 30;
    public int PeriodSeconds { get; set; } = 1;
}
