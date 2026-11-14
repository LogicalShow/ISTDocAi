using Microsoft.AspNetCore.TestHost;
using NUnit.Framework;
using ISTDocAi.Domain.Common.Enums.DocAi;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Extensions.Configuration;
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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.UI.Server.Tests;

public sealed class DocumentIntelligenceReadinessApiTests
{
    
    [TestCase(true)] [TestCase(false)]
    public async Task DI_outcome_controls_readiness_and_cached_calls_but_never_liveness(bool ready)
    {
        using var factory = new ReadinessFactory();
        factory.Analyzer.AccessHandler = _ => Task.FromResult(ready);
        var client = factory.CreateClient();
        PortAssert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        PortAssert.Equal(0, factory.Analyzer.AccessChecks);
        var expected = ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        PortAssert.Equal(expected, (await client.GetAsync("/health/ready")).StatusCode);
        factory.Analyzer.AccessHandler = _ => Task.FromResult(!ready);
        factory.Clock.Now += TimeSpan.FromSeconds(59);
        PortAssert.Equal(expected, (await client.GetAsync("/health/ready")).StatusCode);
        PortAssert.Equal(1, factory.Analyzer.AccessChecks);
        factory.Clock.Now += TimeSpan.FromSeconds(1);
        PortAssert.Equal(ready ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);
        PortAssert.Equal(2, factory.Analyzer.AccessChecks);
        PortAssert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Failed_check_is_503_without_token_or_URL_in_response_or_logs(bool unavailable)
    {
        using var factory = new ReadinessFactory();
        const string sensitive = "https://private.invalid/path?token=PRIVATE_TOKEN";
        factory.Analyzer.AccessHandler = _ => throw (unavailable
            ? new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable)
            : new HttpRequestException(sensitive));
        var response = await factory.CreateClient().GetAsync("/health/ready");
        PortAssert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        PortAssert.DoesNotContain(sensitive, await response.Content.ReadAsStringAsync());
        PortAssert.DoesNotContain(factory.Logs.Lines, line => line.Contains("PRIVATE_TOKEN") || line.Contains("private.invalid"));
    }
    [Test]
    [Category("Machine")]
    public async Task Expired_identity_returns_503_before_dependencies_and_liveness_stays_200()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("DOCAI_TEST_MACHINE") != "1")
        { Assert.Ignore("Set DOCAI_TEST_MACHINE=1 on Windows to run certificate-store, DPAPI and TLS tests."); return; }

        using var factory = new ReadinessFactory();
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=DocAi-readiness-test-" + Guid.NewGuid(), rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(factory.Clock.Now.AddDays(-1), factory.Clock.Now.AddDays(1));
        using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
            generated.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), null,
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.UserKeySet |
            System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet);
        using var store = new System.Security.Cryptography.X509Certificates.X509Store(
            System.Security.Cryptography.X509Certificates.StoreName.My, System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);
        var added = false;
        try
        {
            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);
            store.Add(certificate); added = true;
            factory.Identity = new CertificateIdentity(new IdentityOptions {
                Mode = "ClientCertificate", StoreLocation = "CurrentUser", CertificateThumbprint = certificate.Thumbprint
            }, factory.Clock, false);
            using var client = factory.CreateClient();
            PortAssert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
            factory.Clock.Now += TimeSpan.FromDays(1);
            PortAssert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
            PortAssert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        }
        finally { factory.Identity?.Dispose();
            if (added) store.Remove(certificate);
            using var key = System.Security.Cryptography.X509Certificates.RSACertificateExtensions.GetRSAPrivateKey(certificate);
            if (key is System.Security.Cryptography.RSACng cng) cng.Key.Delete(); }
    }
    [Test]
    [Category("Machine")]
    public async Task Protected_file_without_KeyVault_URL_serves_ready_with_a_real_DPAPI_callback_key()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("DOCAI_TEST_MACHINE") != "1")
        { Assert.Ignore("Set DOCAI_TEST_MACHINE=1 on Windows to run certificate-store, DPAPI and TLS tests."); return; }

        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        var folder = Path.Combine(Path.GetTempPath(), "docai-ready-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "secrets.json");
        try
        {
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(Convert.ToBase64String(new byte[32])),
                Encoding.UTF8.GetBytes("DocAi.WebhookSecrets.v1:LocalMachine"), DataProtectionScope.LocalMachine);
            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string> { ["secret"] = "dpapi:LocalMachine:v1:" + Convert.ToBase64String(encrypted) }));
            using var factory = new ReadinessFactory { ProtectedFilePath = path };
            using var client = factory.CreateClient();
            PortAssert.IsType<ProtectedFileSecretStore>(factory.Services.GetRequiredService<ISecretStore>());
            PortAssert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
            File.Delete(path);
            factory.Services.GetRequiredService<ISecretStore>().InvalidateSigningKey("secret");
            PortAssert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        }
        finally { Directory.Delete(folder, true); }
    }
    private sealed class ReadinessFactory() : BackendFactory(false)
    {
        public Clock Clock { get; } = new();
        public string? ProtectedFilePath { get; set; }
        public CertificateIdentity? Identity { get; set; }
        public WorkerFakes Analyzer { get; } = new();
        public Capture Logs { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("DocAi:Mapping:Source", "Sql");
            builder.UseSetting("DocAi:Identity:Mode", "ManagedIdentity");
            builder.UseSetting("DocAi:Secrets:Provider", "KeyVault");
            builder.UseSetting("DocAi:KeyVault:Url", "https://test.vault.azure.net");
            if (ProtectedFilePath is not null)
            {
                // Registration reads the provider while Program.cs runs; host settings are visible then, late app configuration is not.
                builder.UseSetting("DocAi:Secrets:Provider", "ProtectedFile");
                builder.UseSetting("DocAi:KeyVault:Url", "");
                builder.UseSetting("DocAi:Secrets:ProtectedFile:Path", ProtectedFilePath);
            }
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            builder.ConfigureTestServices(services =>
            {
                if (Identity is not null) { services.RemoveAll<CertificateIdentity>(); services.AddSingleton(Identity); }
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IDocumentStore>(new BlobReady());
                services.RemoveAll<IDocumentAnalyzer>(); services.AddSingleton<IDocumentAnalyzer>(Analyzer);
                services.RemoveAll<ISqlConnectionFactory>(); services.AddSingleton<ISqlConnectionFactory, SqlReady>();
                services.RemoveAll<ISTDocAi.Infrastructure.Services.DocAi.Mapping.MappingProfileReadiness>();
                services.AddSingleton(s => ISTDocAi.Infrastructure.Tests.DocAi.Runtime.MappingReadinessTestBoundary.EmptyProfiles(
                    s.GetRequiredService<ISqlConnectionFactory>(), s.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocAiSqlOptions>>(),
                    s.GetRequiredService<TimeProvider>(), s.GetRequiredService<ILogger<ISTDocAi.Infrastructure.Services.DocAi.Mapping.MappingProfileReadiness>>() ));
                services.RemoveAll<ICallbackDirectory>(); services.AddSingleton<ICallbackDirectory, NoCallbacks>();
                if (ProtectedFilePath is not null) services.AddSingleton<ICallbackDirectory>(new OneCallback());
            });
        }
    }
    // The route only acquires/disposes a connection; SQL factory connectivity is tested separately.
    private sealed class SqlReady : ISqlConnectionFactory
    {
        public Task<SqlConnection> OpenAsync(CancellationToken ct) => Task.FromResult(new SqlConnection());
    }
    private sealed class NoCallbacks : ICallbackDirectory
    {
        public Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CallbackConsumer>>([]);
    }
    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string name) => new Logger(this);
        public void Dispose() { }
        private sealed class Logger(Capture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
                owner.Lines.Enqueue(format(state, error) + error?.ToString());
        }
    }
}
