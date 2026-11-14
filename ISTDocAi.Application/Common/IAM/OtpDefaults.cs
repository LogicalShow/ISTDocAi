namespace ISTDocAi.Application.Common.IAM;

/// <summary>
/// One-time password settings shared by the forgot-password and change-password
/// flows. The Angular forgot-password countdown (OTP_LIFETIME_SECONDS) mirrors
/// <see cref="Lifetime"/>, so keep the two in sync.
/// </summary>
public static class OtpDefaults
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    /// <summary>Human-readable lifetime used in the OTP email body.</summary>
    public const string LifetimeDescription = "1 minute";
}
