using ISTDocAi.Application.Services.DocAi.JobLifecycleService;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ISTDocAi.Application.Tests.DocAi.Services;

public sealed class ServiceRegistrationTests
{
    [Test]
    public void Every_DocAi_service_is_registered_once_as_a_resolvable_singleton()
    {
        var services = new ServiceCollection().AddApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var interfaces = typeof(IJobLifecycleService).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace?.StartsWith(
                "ISTDocAi.Application.Services.DocAi.", StringComparison.Ordinal) == true).ToArray();
        Assert.That(interfaces, Has.Length.EqualTo(20));
        foreach (var contract in interfaces)
        {
            var registrations = services.Where(d => d.ServiceType == contract).ToArray();
            Assert.That(registrations, Has.Length.EqualTo(1), contract.FullName);
            Assert.That(registrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton), contract.FullName);
            Assert.That(scope.ServiceProvider.GetRequiredService(contract), Is.SameAs(provider.GetRequiredService(contract)));
        }
    }
}
