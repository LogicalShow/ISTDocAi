using System.Globalization;

namespace ISTDocAi.OperatorTool;

public sealed record ReportPeriod(DateOnly From, DateOnly To, string Zone, TimeZoneInfo TimeZone)
{
    public string ZoneName => Zone == "egypt" ? "Egypt" : "Saudi Arabia";
    public DateTime FromUtc => Boundary(From.ToDateTime(TimeOnly.MinValue), TimeZone);
    public DateTime ToExclusiveUtc => Boundary(To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeZone);
    public string LocalLine => FormattableString.Invariant($"Period: {From:yyyy-MM-dd} to {To:yyyy-MM-dd} ({ZoneName} time)");
    public string UtcLine => FormattableString.Invariant($"UTC: {FromUtc:yyyy-MM-dd HH:mm} to {ToExclusiveUtc:yyyy-MM-dd HH:mm} (end exclusive)");
    public string LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    public static TimeZoneInfo Resolve(string zone)
    {
        var ids = zone switch
        {
            "egypt" => new[] { "Egypt Standard Time", "Africa/Cairo" },
            "saudi" => new[] { "Arab Standard Time", "Asia/Riyadh" },
            _ => throw new ArgumentException("Time zone must be egypt or saudi.")
        };
        try { return TimeZoneInfo.FindSystemTimeZoneById(ids[0]); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(ids[1]); }
    }
    public static ReportPeriod Parse(string from, string to, string zone)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("Dates must use yyyy-MM-dd.");
        if (start > end) throw new ArgumentException("From date must not be later than To date.");
        if (end.DayNumber - start.DayNumber + 1 > 3660 || end == DateOnly.MaxValue) throw new ArgumentException("Period must contain at most 3660 days and allow an end-exclusive boundary.");
        return new(start, end, zone, Resolve(zone));
    }
    public static DateTime Boundary(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            // Locate the exact first valid tick, without assuming a one-hour transition.
            var low = local.Ticks;
            var high = local.AddDays(1).Ticks;
            while (high - low > 1)
            {
                var middle = low + (high - low) / 2;
                if (zone.IsInvalidTime(new DateTime(middle))) low = middle; else high = middle;
            }
            local = new DateTime(high, DateTimeKind.Unspecified);
        }
        if (zone.IsAmbiguousTime(local)) return new DateTime(local.Ticks - zone.GetAmbiguousTimeOffsets(local).Max().Ticks, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
