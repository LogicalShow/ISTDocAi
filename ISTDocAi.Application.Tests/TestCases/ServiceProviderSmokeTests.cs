using Microsoft.Extensions.Time.Testing;

namespace ISTDocAi.Application.Tests.TestCases;

[TestFixture]
public class ServiceProviderSmokeTests : TestCaseBase
{
    [Test, Order(1)]
    public void ServiceProvider_ShouldResolveMediator()
    {
        // Arrange
        using var scope = this.CreateServiceProviderScope();

        // Act
        var mediator = scope.ServiceProvider.GetService<IMediator>();

        // Assert
        Assert.That(mediator, Is.Not.Null);
    }

    [Test, Order(2)]
    public void ServiceProvider_ShouldResolveTestUserService()
    {
        // Arrange
        using var scope = this.CreateServiceProviderScope();

        // Act
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Assert
        Assert.That(userService, Is.InstanceOf<TestUserService>());
        Assert.That(userService.UserId, Is.EqualTo("admin"));
    }

    [Test, Order(3)]
    public void ServiceProvider_ShouldResolveSystemTimeProviderByDefault()
    {
        // Arrange
        using var scope = this.CreateServiceProviderScope();

        // Act
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        // Assert
        Assert.That(timeProvider, Is.SameAs(TimeProvider.System));
    }

    [Test, Order(4)]
    public void ServiceProvider_ShouldResolveOverriddenFakeTimeProvider()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        using var scope = this.CreateServiceProviderScope(services => services.AddSingleton<TimeProvider>(fake));

        // Act
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        // Assert
        Assert.That(timeProvider, Is.SameAs(fake));
    }
}
