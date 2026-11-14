namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Bound from the "ReportToken" configuration section. Read through IOptionsMonitor,
/// so editing appsettings.json takes effect on the next issued token without a
/// redeploy or restart; the default here applies when the section is missing.
/// </summary>
public sealed class ReportTokenOptions
{
    public const string SectionName = "ReportToken";

    public const int MinLifetimeSeconds = 30;
    public const int MaxLifetimeSeconds = 900;

    /// <summary>
    /// How long a report token stays valid. Keep it short: it only has to survive the
    /// round trip from the click to the report tab's form POST.
    /// Values outside 30-900 seconds are clamped.
    /// </summary>
    public int LifetimeSeconds { get; set; } = 120;
}
