using Microsoft.Extensions.Time.Testing;

namespace ISTDocAi.Application.Tests.TestCases.Time;

/// <summary>
/// Documents why the code uses <c>timeProvider.GetLocalNow().DateTime</c> and not <c>.LocalDateTime</c>:
/// <list type="bullet">
/// <item><c>.DateTime</c> follows <see cref="TimeProvider.LocalTimeZone"/>, which a test can set.</item>
/// <item><c>.LocalDateTime</c> always converts to the machine zone (<see cref="TimeZoneInfo.Local"/>), whatever the provider says.</item>
/// <item><see cref="TimeProvider.System"/> cannot be given another zone at all.</item>
/// </list>
/// Test zones are derived from the machine zone, so they always differ from it and the assertions hold on any machine.
/// </summary>
[TestFixture]
public class ProviderZoneVsMachineZoneTests
{
    private static readonly DateTimeOffset UtcInstant = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void DateTime_ShouldUseZoneSetOnProvider()
    {
        // Arrange
        var zone = CreateZoneDifferentFromMachine(hoursFromMachine: 5);
        var timeProvider = new FakeTimeProvider(UtcInstant);
        timeProvider.SetLocalTimeZone(zone);

        // Act
        var local = timeProvider.GetLocalNow().DateTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(local, Is.EqualTo(WallClockIn(zone)));
            Assert.That(local, Is.Not.EqualTo(WallClockIn(TimeZoneInfo.Local)));
            Assert.That(local.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void LocalDateTime_ShouldIgnoreZoneSetOnProvider_AndUseMachineZone()
    {
        // Arrange
        var zone = CreateZoneDifferentFromMachine(hoursFromMachine: 5);
        var timeProvider = new FakeTimeProvider(UtcInstant);
        timeProvider.SetLocalTimeZone(zone);

        // Act
        var local = timeProvider.GetLocalNow().LocalDateTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(local, Is.EqualTo(WallClockIn(TimeZoneInfo.Local)));
            Assert.That(local, Is.Not.EqualTo(WallClockIn(zone)), "Zone set on the provider was ignored.");
            Assert.That(local.Kind, Is.EqualTo(DateTimeKind.Local));
        });
    }

    [Test]
    public void ChangingProviderZone_ShouldChangeDateTime_ButNotLocalDateTime()
    {
        // Arrange
        var firstZone = CreateZoneDifferentFromMachine(hoursFromMachine: 3);
        var secondZone = CreateZoneDifferentFromMachine(hoursFromMachine: 5);
        var timeProvider = new FakeTimeProvider(UtcInstant);

        // Act
        timeProvider.SetLocalTimeZone(firstZone);
        var dateTimeInFirstZone = timeProvider.GetLocalNow().DateTime;
        var localDateTimeInFirstZone = timeProvider.GetLocalNow().LocalDateTime;

        timeProvider.SetLocalTimeZone(secondZone);
        var dateTimeInSecondZone = timeProvider.GetLocalNow().DateTime;
        var localDateTimeInSecondZone = timeProvider.GetLocalNow().LocalDateTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(dateTimeInFirstZone, Is.EqualTo(WallClockIn(firstZone)));
            Assert.That(dateTimeInSecondZone, Is.EqualTo(WallClockIn(secondZone)));
            Assert.That(dateTimeInSecondZone, Is.Not.EqualTo(dateTimeInFirstZone), ".DateTime should follow the provider zone.");
            Assert.That(localDateTimeInSecondZone, Is.EqualTo(localDateTimeInFirstZone), ".LocalDateTime should stay on the machine zone.");
        });
    }

    [Test]
    public void SystemTimeProvider_ShouldAlwaysUseMachineZone_AndHaveNoWayToChangeIt()
    {
        // Arrange
        var system = TimeProvider.System;

        // Act
        var localDateTime = system.GetLocalNow().LocalDateTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(system.LocalTimeZone.Id, Is.EqualTo(TimeZoneInfo.Local.Id));
            Assert.That(localDateTime, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(5)), "Real machine clock and zone.");
            Assert.That(system.GetType().GetMethod(nameof(FakeTimeProvider.SetLocalTimeZone)), Is.Null, "No SetLocalTimeZone.");
            Assert.That(system.GetType().GetMethod(nameof(FakeTimeProvider.Advance)), Is.Null, "No Advance.");
            Assert.That(system.GetType().GetProperty(nameof(TimeProvider.LocalTimeZone))!.CanWrite, Is.False, "LocalTimeZone is read-only.");
        });
    }

    private static DateTime WallClockIn(TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(UtcInstant.UtcDateTime, zone);

    /// <summary>Fixed-offset zone that is guaranteed to differ from the machine zone at <see cref="UtcInstant"/>.</summary>
    private static TimeZoneInfo CreateZoneDifferentFromMachine(int hoursFromMachine)
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(UtcInstant) + TimeSpan.FromHours(hoursFromMachine);

        // Keep within the valid UTC offset range (-14h..+14h)
        if (offset > TimeSpan.FromHours(14)) offset -= TimeSpan.FromHours(24);
        if (offset < TimeSpan.FromHours(-14)) offset += TimeSpan.FromHours(24);

        var id = $"NotMachineZone{hoursFromMachine}";
        return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
    }
}
