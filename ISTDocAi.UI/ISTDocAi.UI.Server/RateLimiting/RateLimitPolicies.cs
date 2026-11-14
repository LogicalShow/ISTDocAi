namespace ISTDocAi.UI.Server.RateLimiting;

/// <summary>Names used with <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Queued concurrency limit for the widget fan-out of the dashboard page.</summary>
    public const string Dashboard = "dashboard";

    /// <summary>Tight window on the unauthenticated auth endpoints (login, OTP, reset).</summary>
    public const string Auth = "auth";
}
