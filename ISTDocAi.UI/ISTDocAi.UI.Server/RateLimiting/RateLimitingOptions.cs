namespace ISTDocAi.UI.Server.RateLimiting;

/// <summary>
/// Bound from the "RateLimiting" configuration section. Every limit is configurable
/// so a deployment can be tuned without a rebuild; the defaults here are what the
/// app runs with when the section is missing.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Kill switch - when false no limiter is registered at all.</summary>
    public bool Enabled { get; set; } = true;

    public GlobalLimitOptions Global { get; set; } = new();

    public QueuedLimitOptions Dashboard { get; set; } = new();

    public WindowLimitOptions Auth { get; set; } = new();

    public sealed class GlobalLimitOptions
    {
        /// <summary>Requests allowed per caller inside <see cref="WindowSeconds"/>.</summary>
        public int PermitLimit { get; set; } = 300;

        public int WindowSeconds { get; set; } = 60;

        /// <summary>Sliding-window segments - more segments means a smoother window.</summary>
        public int SegmentsPerWindow { get; set; } = 6;

        /// <summary>Small queue so a short burst waits instead of failing outright.</summary>
        public int QueueLimit { get; set; } = 20;
    }

    /// <summary>
    /// Concurrency limit used by the dashboard: the page fans out one request per
    /// widget, so the excess is queued rather than rejected - opening the page must
    /// never fail with 429.
    /// </summary>
    public sealed class QueuedLimitOptions
    {
        public int PermitLimit { get; set; } = 4;

        public int QueueLimit { get; set; } = 60;
    }

    public sealed class WindowLimitOptions
    {
        public int PermitLimit { get; set; } = 10;

        public int WindowSeconds { get; set; } = 60;

        public int QueueLimit { get; set; } = 0;
    }
}
