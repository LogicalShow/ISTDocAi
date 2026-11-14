using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ISTDocAi.ArchitectureTests.TestCases;

public sealed class DocAiWebhookBoundaryTests
{
    [Test]
    public void Complete_DocAi_host_has_exactly_the_six_expected_background_services()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddDocAiDatabase(configuration, false);
        services.AddDocAiAzure(new Environment(), configuration);
        services.AddDocAiWorker(configuration);
        var expected = new[] { typeof(DatabaseInitializationService), typeof(CertificateExpiryService),
            typeof(JobProcessorService), typeof(WebhookSenderService), typeof(RetentionService), typeof(SignalsService) };
        Assert.That(services.Where(d => d.ServiceType == typeof(IHostedService)).Select(d => d.ImplementationType), Is.EqualTo(expected));
        foreach (var type in expected)
        {
            Assert.That(typeof(BackgroundService).IsAssignableFrom(type), Is.True, type.Name);
            Assert.That(type.GetInterfaces().Any(i => i.Namespace?.StartsWith("Quartz", StringComparison.Ordinal) == true), Is.False);
        }
        Assert.That(services.Any(d => d.ServiceType.FullName?.Contains("Quartz", StringComparison.Ordinal) == true || d.ServiceType.Name.Contains("TaskScheduler", StringComparison.Ordinal)), Is.False);
        Assert.That(services.Single(d => d.ServiceType == typeof(DeliverWebhook)).Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(services.Count(d => d.ServiceType == typeof(SecretFailureLog)), Is.EqualTo(1));
        Assert.That(services.Single(d => d.ServiceType == typeof(SecretFailureLog)).Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        Assert.That(services.Single(d => d.ServiceType == typeof(IDocAiV2Json)).Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public void Delivery_is_not_a_MediatR_request_and_Job_uses_the_GET_DTO()
    {
        Assert.That(typeof(DeliverWebhook).GetInterfaces().Any(i => i.Namespace?.StartsWith("MediatR", StringComparison.Ordinal) == true), Is.False);
        Assert.That(typeof(WebhookBody).GetProperty("Job")!.PropertyType, Is.EqualTo(typeof(GetJobResponse)));
        var visited = new HashSet<Type>();
        void Inspect(Type type)
        {
            if (!visited.Add(type)) return;
            if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Inspect(argument);
            if (type.IsArray) { Inspect(type.GetElementType()!); return; }
            if (type.Namespace?.StartsWith("ISTDocAi.Application", StringComparison.Ordinal) != true) return;
            foreach (var property in type.GetProperties())
            {
                Assert.That(property.Name.Contains("Usd", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("AzureCost", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("meter", StringComparison.OrdinalIgnoreCase), Is.False, type.Name + "." + property.Name);
                Inspect(property.PropertyType);
            }
        }
        Inspect(typeof(WebhookBody));
    }

    [Test]
    public void Application_has_no_Azure_ProtectedData_or_SqlClient_dependency()
    {
        Assert.That(Assemblies.Application.GetReferencedAssemblies().Select(a => a.Name)
            .Where(n => n!.StartsWith("Azure.", StringComparison.Ordinal) || n.Contains("ProtectedData", StringComparison.Ordinal) || n.Contains("SqlClient", StringComparison.Ordinal)), Is.Empty);
        Assert.That(Types.InAssembly(Assemblies.Application).ShouldNot().HaveDependencyOnAny("Azure", "System.Security.Cryptography.ProtectedData", "Microsoft.Data.SqlClient", "System.Data.SqlClient").GetResult().IsSuccessful, Is.True);
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ArchitectureTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
