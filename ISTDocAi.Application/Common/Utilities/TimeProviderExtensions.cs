namespace ISTDocAi.Application.Common.Utilities;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Injectable replacement for <see cref="DateTime.Now"/>: wall-clock time in
    /// <see cref="TimeProvider.LocalTimeZone"/>.
    /// </summary>
    /// <remarks>
    /// Kind is <see cref="DateTimeKind.Unspecified"/> on purpose: <see cref="DateTimeKind.Local"/> means the machine's
    /// <see cref="TimeZoneInfo.Local"/>, which can differ from the provider's zone.
    /// Don't call <see cref="DateTime.ToUniversalTime"/>/<see cref="DateTime.ToLocalTime"/> on the result;
    /// convert with <c>TimeZoneInfo.ConvertTimeToUtc(value, timeProvider.LocalTimeZone)</c>.
    /// </remarks>
    public static DateTime GetLocalDateTime(this TimeProvider timeProvider) =>
        timeProvider.GetLocalNow().DateTime;
}
