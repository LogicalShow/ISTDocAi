using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure;

public static class DocAiWorkerRegistration
{
    public static IServiceCollection AddDocAiWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkerOptions>().Bind(configuration.GetSection("Worker"))
            .Validate(o => o.MaxParallelJobs is >= 1 and <= 16, "Worker:MaxParallelJobs must be between 1 and 16.")
            .ValidateOnStart();
        // 30s begin + 100s POST + 30s accepted + 30s release + 20s margin.
        // Best effort only: Linux App Service may terminate sooner; persisted leases recover work.
        services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(210));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IWorkerCircuitBreaker, WorkerCircuitBreaker>();
        services.AddScoped<ProcessJob>();
        services.AddHostedService<JobProcessorService>();
        services.AddOptions<WebhookOptions>().Bind(configuration.GetSection("Webhooks"))
            .Validate(o => o.MaxParallelSends is >= 1 and <= 16, "Webhooks:MaxParallelSends must be between 1 and 16.").ValidateOnStart();
        services.TryAddSingleton<SecretFailureLog>();
        services.AddScoped<DeliverWebhook>();
        services.TryAddSingleton<ConsumerWebhookEvents>();
        services.TryAddSingleton<IConsumerWebhookEvents>(s => s.GetRequiredService<ConsumerWebhookEvents>());
        services.AddHostedService<WebhookSenderService>();
        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection("Retention"))
            .Validate(o => o.RunAtUtc >= TimeSpan.Zero && o.RunAtUtc < TimeSpan.FromDays(1),
                "Retention:RunAtUtc must be a UTC time of day.").ValidateOnStart();
        services.AddHostedService<RetentionService>();
        services.AddOptions<SignalsOptions>().Bind(configuration.GetSection("Signals"));
        services.AddHostedService<SignalsService>();
        services.TryAddSingleton<IDocAiV2Json, DocAiV2Json>();
        return services;
    }
}
