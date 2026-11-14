using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
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
using ISTDocAi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class WebhookRegistrationTests
{
    [Test]
    public void Registered_named_client_disables_redirects_and_has_no_certificate_bypass()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDocAiAzure(new EnvironmentStub(), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DocAi:KeyVault:Url"] = "https://test.vault.azure.net" }).Build());
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("webhooks");
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var sockets = PortAssert.IsType<SocketsHttpHandler>(handler);
        PortAssert.False(sockets.AllowAutoRedirect); PortAssert.Null(sockets.SslOptions.RemoteCertificateValidationCallback);
        PortAssert.Equal(TimeSpan.FromSeconds(10), sockets.ConnectTimeout);
        PortAssert.Equal(TimeSpan.FromMinutes(5), sockets.PooledConnectionLifetime);
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("webhooks");
        PortAssert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }
    [Test]
    public async Task Vault_configuration_is_validated_on_host_start()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DocAi:Secrets:Provider"] = "KeyVault", ["DocAi:KeyVault:Url"] = "http://invalid.example" });
        builder.Services.AddDocAiAzure(builder.Environment, builder.Configuration);
        using var host = builder.Build();
        await PortAssert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
    [Test]
    public async Task Protected_file_without_vault_url_starts_and_callback_readiness_works()
    {
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DocAi:Secrets:Provider"] = "ProtectedFile", ["DocAi:KeyVault:Url"] = "",
            ["DocAi:Secrets:ProtectedFile:Path"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json") });
        builder.Services.AddDocAiAzure(builder.Environment, builder.Configuration);
        var callbacks = new WebhookFakes { Callback = null };
        builder.Services.AddSingleton<ICallbackDirectory>(callbacks);
        using var host = builder.Build();
        if (!OperatingSystem.IsWindows())
        {
            var error = await PortAssert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
            PortAssert.Contains("Windows DPAPI", error.Message); return;
        }
        await host.StartAsync();
        PortAssert.IsType<ProtectedFileSecretStore>(host.Services.GetRequiredService<ISecretStore>());
        PortAssert.True(await host.Services.GetRequiredService<WebhookReadiness>().CheckAsync(default));
        await host.StopAsync();
    }
    [Test]
    public async Task Unknown_provider_is_rejected_on_start()
    {
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DocAi:Secrets:Provider"] = "Typo" });
        builder.Services.AddDocAiAzure(builder.Environment, builder.Configuration);
        using var host = builder.Build();
        await PortAssert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
