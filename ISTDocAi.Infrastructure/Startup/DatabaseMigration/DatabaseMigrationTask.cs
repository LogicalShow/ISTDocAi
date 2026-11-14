using ISTDocAi.Application.Startup;
using ISTDocAi.Application.Startup.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ISTDocAi.Infrastructure.Data;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration;

public class DatabaseMigrationTask : IStartupTask
{
    public StartupStage Stage => StartupStage.DatabaseMigration;
    
    public int Order => DatabaseMigrationOrder.DatabaseMigration;

    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying database migrations.");
            throw;
        }
    }
}
