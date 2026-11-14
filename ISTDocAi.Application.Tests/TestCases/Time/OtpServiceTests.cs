using ISTDocAi.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;

namespace ISTDocAi.Application.Tests.TestCases.Time;

[TestFixture]
public class OtpServiceTests
{
    private static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(5);

    private FakeTimeProvider _timeProvider = null!;
    private OtpService _otpService = null!;
    private string _email = null!;

    [SetUp]
    public void SetUp()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        _otpService = new OtpService(_timeProvider);

        // OTP store is static: isolate every test with its own key
        _email = $"{Guid.NewGuid():N}@test.local";
    }

    [TearDown]
    public void TearDown()
    {
        _otpService.RemoveOtp(_email);
    }

    [Test]
    public void TryGetOtp_BeforeExpiry_ShouldReturnOtp()
    {
        // Arrange
        _otpService.SaveOtp(_email, "123456", ValidFor);

        // Act
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var found = _otpService.TryGetOtp(_email, out var otp);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(otp, Is.EqualTo("123456"));
        });
    }

    [Test]
    public void TryGetOtp_AtExactExpiry_ShouldReturnOtp()
    {
        // Arrange
        _otpService.SaveOtp(_email, "123456", ValidFor);

        // Act
        _timeProvider.Advance(ValidFor);
        var found = _otpService.TryGetOtp(_email, out _);

        // Assert
        Assert.That(found, Is.True);
    }

    [Test]
    public void TryGetOtp_AfterExpiry_ShouldReturnFalse()
    {
        // Arrange
        _otpService.SaveOtp(_email, "123456", ValidFor);

        // Act
        _timeProvider.Advance(ValidFor + TimeSpan.FromSeconds(1));
        var found = _otpService.TryGetOtp(_email, out var otp);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(otp, Is.Empty);
        });
    }
}
