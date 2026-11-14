using Microsoft.Extensions.Configuration;
using ISTDocAi.Infrastructure;

namespace ISTDocAi.Application.Tests.Common;

public class ServiceProviderFactory
{
    /// <param name="configure">Optional overrides applied last, e.g. registering a FakeTimeProvider.</param>
    public static IServiceScope CreateServiceProviderScope(Action<IServiceCollection>? configure = null)
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        // Get the connection string from configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Set environment variables for testing
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", configuration.GetValue<string>("Secrets:JWT_SECRET_KEY"));

        // Configure services
        var services = new ServiceCollection();

        // Add DependencyInjection: Application
        services.AddApplication();

        // Add DependencyInjection: Infrastructure
        services.AddInfrastructure(connectionString);

        // simulate logged-in user service
        services.AddScoped<IUserService, TestUserService>();

        configure?.Invoke(services);

        return services.BuildServiceProvider().CreateScope();
    }
}
