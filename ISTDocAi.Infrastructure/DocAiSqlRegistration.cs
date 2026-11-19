using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
namespace ISTDocAi.Infrastructure;

public static class DocAiSqlRegistration
{
    public static IServiceCollection AddDocAiSql(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDocAiSql(o =>
        {
            configuration.GetSection("DocAi:Sql").Bind(o);
            o.ConnectionString = BackgroundServices.DocAi.DatabaseConfiguration.RuntimeConnection(configuration);
        });

    public static IServiceCollection AddDocAiSql(this IServiceCollection services, Action<DocAiSqlOptions> configure)
    {
        services.AddOptions<DocAiSqlOptions>().Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "A SQL connection string is required.")
            .Validate(o => o.CommandTimeoutSeconds > 0, "Command timeout must be positive.")
            .Validate(o => o.RetentionTimeoutSeconds is >= 60 and <= 7200, "Retention timeout must be between 60 and 7200 seconds.");
        services.TryAddSingleton<SqlStatementCatalog>();
        services.TryAddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.TryAddScoped<SqlExecution>(s => new SqlExecution(s.GetRequiredService<ISqlConnectionFactory>(), s.GetRequiredService<SqlStatementCatalog>(), s.GetRequiredService<IOptions<DocAiSqlOptions>>(), s.GetService<Microsoft.Extensions.Logging.ILogger<SqlExecution>>()));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddScoped<SqlJobRepository>(s => new SqlJobRepository(s.GetRequiredService<SqlExecution>(), s.GetRequiredService<ISqlConnectionFactory>(), s.GetRequiredService<IOptions<DocAiSqlOptions>>(), s.GetRequiredService<IMappingProfileResolver>()));
        services.TryAddScoped<IJobRepository>(s => s.GetRequiredService<SqlJobRepository>());
        services.TryAddScoped<SqlJobQueueRepository>(s => new SqlJobQueueRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IJobQueue>(s => s.GetRequiredService<SqlJobQueueRepository>());
        services.TryAddScoped<SqlOperationStoreRepository>(s => new SqlOperationStoreRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IOperationStore>(s => s.GetRequiredService<SqlOperationStoreRepository>());
        services.TryAddScoped<SqlWebhookOutboxRepository>(s => new SqlWebhookOutboxRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IConsumerWebhookRetry>(s => s.GetRequiredService<SqlWebhookOutboxRepository>());
        services.TryAddScoped<IWebhookOutbox>(s => s.GetRequiredService<SqlWebhookOutboxRepository>());
        services.TryAddScoped<SqlModelCatalogRepository>(s => new SqlModelCatalogRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IModelCatalog>(s => s.GetRequiredService<SqlModelCatalogRepository>());
        services.TryAddScoped<SqlRetentionRepository>(s => new SqlRetentionRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IRetention>(s => s.GetRequiredService<SqlRetentionRepository>());
        services.TryAddScoped<SqlConsumerSourceRepository>(s => new SqlConsumerSourceRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IConsumerSource>(s => s.GetRequiredService<SqlConsumerSourceRepository>());
        services.TryAddScoped<SqlUsageReaderRepository>(s => new SqlUsageReaderRepository(s.GetRequiredService<SqlExecution>(), s.GetRequiredService<ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService.IPageAllowanceStatusService>()));
        services.TryAddScoped<IUsageReader>(s => s.GetRequiredService<SqlUsageReaderRepository>());
        services.TryAddScoped<SqlModelThresholdSourceRepository>(s => new SqlModelThresholdSourceRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<IModelThresholdSource>(s => s.GetRequiredService<SqlModelThresholdSourceRepository>());
        services.TryAddScoped<SqlCallbackSourceRepository>(s => new SqlCallbackSourceRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<ICallbackSource>(s => s.GetRequiredService<SqlCallbackSourceRepository>());
        services.TryAddScoped<SqlSignalReaderRepository>(s => new SqlSignalReaderRepository(s.GetRequiredService<SqlExecution>()));
        services.TryAddScoped<ISignalReader>(s => s.GetRequiredService<SqlSignalReaderRepository>());
        services.TryAddSingleton<IModelThresholdReader, ModelThresholdCache>();
        services.TryAddSingleton<ICallbackDirectory, CallbackDirectory>();
        services.TryAddScoped<IAzurePriceCheckStore>(s => new SqlAzurePriceCheckRepository(s.GetRequiredService<SqlExecution>()));
        return services;
    }
}
