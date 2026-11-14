using ISTDocAi.Application.Startup;
using ISTDocAi.Application.Startup.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ISTDocAi.Infrastructure.Data.Seeders;

namespace ISTDocAi.Infrastructure.Startup.DataInitialization;
public sealed class SeederExecutorTask(ILogger<SeederExecutorTask> logger) : IStartupTask
{
    public StartupStage Stage => StartupStage.DataInitialization;

    public int Order => DataInitializationOrder.Seeders;

    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting seeder execution...");

        var seeders = serviceProvider.GetServices<ISeeder>()
            .OrderBy(s => s.Order)
            .ThenBy(s => s.GetType().Name)
            .ToList();

        if (!seeders.Any())
        {
            logger.LogWarning("No seeders registered.");
            return;
        }

        logger.LogInformation("Found {Count} seeder(s) to execute.", seeders.Count);

        foreach (var seeder in seeders)
        {
            var seederName = seeder.GetType().Name;
            var order = seeder.Order;

            logger.LogInformation("[Order: {Order}] Executing seeder: {SeederName}", order, seederName);

            try
            {
                await seeder.SeedAsync(serviceProvider);
                logger.LogDebug("[Order: {Order}] Seeder {SeederName} completed successfully", order, seederName);
            }
            catch (Exception)
            {
                logger.LogError("{event}", "database.seeding_failed");
                throw;
            }
        }

        logger.LogInformation("All seeders executed successfully.");
    }
}
