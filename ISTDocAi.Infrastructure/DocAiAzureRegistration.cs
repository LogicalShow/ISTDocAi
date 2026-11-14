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

public static class DocAiAzureRegistration
{
    public static IServiceCollection AddDocAiAzure(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        services.Configure<BlobOptions>(configuration.GetSection("DocAi:Blob"));
        services.AddOptions<IdentityOptions>().Bind(configuration.GetSection("DocAi:Identity"))
            .Validate(IdentityOptions.IsValid, "DocAi:Identity requires a valid mode, store location, and certificate tenant/client IDs and thumbprint.").ValidateOnStart();
        services.TryAddSingleton(s => new CertificateIdentity(s.GetRequiredService<IOptions<IdentityOptions>>().Value,
            s.GetRequiredService<TimeProvider>(), environment.IsDevelopment()));
        services.AddHostedService<CertificateExpiryService>();
        services.TryAddSingleton<TokenCredential>(s => IdentityCredentialFactory.Create(
            s.GetRequiredService<IOptions<IdentityOptions>>().Value, environment.IsDevelopment(),
            s.GetRequiredService<IOptions<BlobOptions>>().Value.ManagedIdentityClientId, s.GetRequiredService<CertificateIdentity>()));
        services.TryAddSingleton<IDocumentStore, BlobDocumentStore>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<DocumentIntelligenceOptions>(configuration.GetSection("DocAi:DocumentIntelligence"));
        services.TryAddSingleton<IRawResultStore, BlobRawResultStore>();
        services.TryAddSingleton<BearerTokenCache>();
        services.AddTransient<BearerTokenHandler>();
        services.TryAddSingleton<AnalysisRateLimits>();
        services.TryAddSingleton<DocumentIntelligenceReadiness>();
        services.AddHttpClient<IDocumentAnalyzer, DocumentIntelligenceRestClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            .AddHttpMessageHandler<BearerTokenHandler>()
            .RedactLoggedHeaders(_ => true)
            .RemoveAllLoggers();
        services.AddOptions<SecretsOptions>().Bind(configuration.GetSection("DocAi:Secrets"))
            .Validate(o => o.Provider is "ProtectedFile" or "KeyVault", "DocAi:Secrets:Provider must be ProtectedFile or KeyVault.")
            .Validate(o => o.Provider != "ProtectedFile" || OperatingSystem.IsWindows(), "ProtectedFile secrets require Windows DPAPI.")
            .Validate(o => o.Provider != "ProtectedFile" || Path.IsPathFullyQualified(o.ProtectedFile.Path),
                "DocAi:Secrets:ProtectedFile:Path must be an absolute path.").ValidateOnStart();
        var provider = configuration["DocAi:Secrets:Provider"] ?? "ProtectedFile";
        if (provider == "KeyVault")
        {
            services.AddOptions<KeyVaultOptions>().Bind(configuration.GetSection("DocAi:KeyVault"))
                .Validate(KeyVaultOptions.IsValid, "DocAi:KeyVault:Url must be an HTTPS Azure Key Vault URL.").ValidateOnStart();
            services.TryAddSingleton(s => new SecretClient(new Uri(s.GetRequiredService<IOptions<KeyVaultOptions>>().Value.Url),
                s.GetRequiredService<TokenCredential>(), new SecretClientOptions { Diagnostics = { IsLoggingEnabled = false, IsLoggingContentEnabled = false } }));
            services.TryAddSingleton<ISecretStore, KeyVaultSecretStore>();
        }
        else
        {
            services.TryAddSingleton<ISecretStore, ProtectedFileSecretStore>();
        }
        services.TryAddSingleton<ICallbackDirectory, CallbackDirectory>();
        services.TryAddSingleton<SecretFailureLog>();
        services.TryAddSingleton<WebhookReadiness>();
        services.TryAddSingleton<IWebhookTransport, WebhookHttpTransport>();
        services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(WebhookHttpTransport.CreateHandler)
            .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
        return services;
    }
}
