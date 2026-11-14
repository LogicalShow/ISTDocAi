using ISTDocAi.Application.Common.Utilities;
using Microsoft.Extensions.Time.Testing;

namespace ISTDocAi.Application.Tests.TestCases.Time;

[TestFixture]
public class TimeProviderExtensionsTests
{
    // Custom zone so the result never depends on the OS time-zone database or the machine's zone
    private static readonly TimeZoneInfo PlusFourZone =
        TimeZoneInfo.CreateCustomTimeZone("Test+04", TimeSpan.FromHours(4), "Test+04", "Test+04");

    [Test]
    public void GetLocalDateTime_ShouldReturnWallClockOfProviderZone_WithUnspecifiedKind()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        timeProvider.SetLocalTimeZone(PlusFourZone);

        // Act
        var local = timeProvider.GetLocalDateTime();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(local, Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 0)));
            Assert.That(local.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void GetLocalDateTime_ConvertedWithProviderZone_ShouldRoundTripToUtcInstant()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(utcNow);
        timeProvider.SetLocalTimeZone(PlusFourZone);

        // Act
        var utc = TimeZoneInfo.ConvertTimeToUtc(timeProvider.GetLocalDateTime(), timeProvider.LocalTimeZone);

        // Assert
        Assert.That(utc, Is.EqualTo(utcNow.UtcDateTime));
    }
}
