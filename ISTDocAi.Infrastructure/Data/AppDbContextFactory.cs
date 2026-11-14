using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ISTDocAi.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configurationBasePath = GetConfigurationBasePath();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(configurationBasePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddJsonFile("appsettings.Local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            ISTDocAi.Infrastructure.BackgroundServices.DocAi.DatabaseConfiguration.RejectRemovedConnection(configuration);
            var connectionString = string.IsNullOrWhiteSpace(configuration["DocAi:Database:MigrationConnectionString"])
                ? ISTDocAi.Infrastructure.BackgroundServices.DocAi.DatabaseConfiguration.RuntimeConnection(configuration)
                : configuration["DocAi:Database:MigrationConnectionString"];
            DatabaseConnection.Validate(connectionString);

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                    .MigrationsHistoryTable("__DocAiIamMigrationsHistory", "dbo"));

            return new AppDbContext(optionsBuilder.Options);
        }

        private static string GetConfigurationBasePath()
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (currentDirectory is not null)
            {
                var localAppSettingsPath = Path.Combine(currentDirectory.FullName, "appsettings.json");
                if (File.Exists(localAppSettingsPath))
                {
                    return currentDirectory.FullName;
                }

                var serverAppSettingsPath = Path.Combine(
                    currentDirectory.FullName,
                    "ISTDocAi.UI",
                    "ISTDocAi.UI.Server",
                    "appsettings.json");

                if (File.Exists(serverAppSettingsPath))
                {
                    return Path.GetDirectoryName(serverAppSettingsPath)!;
                }

                currentDirectory = currentDirectory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
