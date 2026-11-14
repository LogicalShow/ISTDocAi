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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using ISTDocAi.Infrastructure;

using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class CertificateIdentityTests
{
    [Test]
    public void Development_certificate_uses_configured_loader_and_certificate_credential()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=DevSetup-unit", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var options = Options(generated.Thumbprint);
        var calls = 0;
        using var identity = new CertificateIdentity(options, TimeProvider.System, true, actual =>
        {
            Assert.That(actual.StoreLocation, Is.EqualTo("CurrentUser")); calls++;
            return new X509Certificate2(generated);
        });
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(IdentityCredentialFactory.Create(options, true, null, identity), Is.TypeOf<ClientCertificateCredential>());
    }
    [TestCase("ManagedIdentity", true)]
    [TestCase("ClientCertificate", false)]
    public void Development_default_or_invalid_certificate_settings_keep_default_credential(string mode, bool valid)
    {
        var options = Options(new string('A',40)); options.Mode=mode;
        if(!valid) options.TenantId="invalid";
        using var identity = new CertificateIdentity(options, TimeProvider.System, true, _ => throw new AssertionException("Store must not be accessed"));
        Assert.That(IdentityCredentialFactory.Create(options,true,null,identity),Is.TypeOf<DefaultAzureCredential>());
    }

    private static IdentityOptions Options(string thumbprint) => new()
    {
        Mode = "ClientCertificate", StoreLocation = "CurrentUser", TenantId = Guid.NewGuid().ToString(),
        ClientId = Guid.NewGuid().ToString(), CertificateThumbprint = thumbprint
    };
    
    [TestCase("ManagedIdentity", "LocalMachine", true)]
    [TestCase("ClientCertificate", "CurrentUser", true)]
    [TestCase("Unknown", "CurrentUser", false)]
    [TestCase("ClientCertificate", "Other", false)]
    public void Options_validate_mode_and_store(string mode, string store, bool expected)
    {
        var options = Options(new string('A', 40)); options.Mode = mode; options.StoreLocation = store;
        PortAssert.Equal(expected, IdentityOptions.IsValid(options));
    }
    
    [TestCase("TenantId")][TestCase("ClientId")][TestCase("CertificateThumbprint")]
    public void Certificate_settings_are_required(string property)
    {
        var options = Options(new string('A', 40));
        typeof(IdentityOptions).GetProperty(property)!.SetValue(options, "invalid");
        PortAssert.False(IdentityOptions.IsValid(options));
    }
    
    [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)]
    [Category("Machine")]
    public void Store_requires_valid_certificate_with_private_key(bool privateKey, bool expired)
    {
        MachineGate.Require();
        var clock = new TestClock();
        WithCertificate(clock, privateKey, expired, options =>
        {
            if (!privateKey || expired)
            {
                var error = PortAssert.Throws<InvalidOperationException>(() => new CertificateIdentity(options, clock, false));
                PortAssert.DoesNotContain(options.CertificateThumbprint, error.Message);
                return;
            }
            using var identity = new CertificateIdentity(options, clock, false);
            PortAssert.True(identity.Certificate!.HasPrivateKey);
            PortAssert.True(identity.IsReady);
            PortAssert.IsType<ClientCertificateCredential>(IdentityCredentialFactory.Create(options, false, null, identity));
            PortAssert.IsType<ClientCertificateCredential>(IdentityCredentialFactory.Create(options, true, null, identity));
        });
    }
    [Test]
    [Category("Machine")]
    public void Missing_certificate_fails_and_managed_identity_and_development_need_no_store()
    {
        MachineGate.Require();
        var options = Options(new string('0', 40));
        PortAssert.Throws<InvalidOperationException>(() => new CertificateIdentity(options, TimeProvider.System, false));
        options.TenantId = "invalid";
        using var development = new CertificateIdentity(options, TimeProvider.System, true);
        PortAssert.IsType<DefaultAzureCredential>(IdentityCredentialFactory.Create(options, true, null, development));
        options.Mode = "ManagedIdentity";
        using var managed = new CertificateIdentity(options, TimeProvider.System, false);
        PortAssert.IsType<ManagedIdentityCredential>(IdentityCredentialFactory.Create(options, false, null, managed));
        PortAssert.True(managed.IsReady);
    }
    [Test]
    [Category("Machine")]
    public async Task Warning_at_start_and_daily_and_readiness_expires_using_fake_clock()
    {
        MachineGate.Require();
        var clock = new TestClock();
        await WithCertificateAsync(clock, async options =>
        {
            using var identity = new CertificateIdentity(options, clock, false);
            var logger = new CheckLogger();
            var serviceClock = new TimerReadyClock(clock);
            using var service = new CertificateExpiryService(identity, serviceClock, logger);
            await service.StartAsync(CancellationToken.None);
            PortAssert.Empty(logger.Lines);
            await serviceClock.TimerReady.Task.WaitAsync(TimeSpan.FromSeconds(30));
            // Initial certificate has 40 days remaining; under 30 triggers a warning.
            clock.Advance(TimeSpan.FromDays(11));
            PortAssert.Equal("identity.certificate_expiring daysLeft=29", await logger.NextAsync());
            PortAssert.Contains(logger.Lines, x => x.Level == LogLevel.Warning && x.Text == "identity.certificate_expiring daysLeft=29");
            using var atStart = new CertificateExpiryService(identity, serviceClock, logger);
            await atStart.StartAsync(CancellationToken.None);
            PortAssert.Equal("identity.certificate_expiring daysLeft=29", await logger.NextAsync());
            PortAssert.True(logger.Lines.Count >= 2);
            PortAssert.True(identity.IsReady);
            clock.Advance(TimeSpan.FromDays(29));
            PortAssert.False(identity.IsReady);
            await atStart.StopAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        });
    }
    // A registered timer signal prevents advancing the fake clock before BackgroundService starts.
    private sealed class TimerReadyClock(TestClock inner) : TimeProvider
    {
        public TaskCompletionSource TimerReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            TimerReady.TrySetResult();
            return timer;
        }
    }
    private sealed class CheckLogger : ILogger<CertificateExpiryService>
    {
        private readonly System.Threading.Channels.Channel<string> checks = System.Threading.Channels.Channel.CreateUnbounded<string>();
        public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Text)> Lines { get; } = new();
        public async Task<string> NextAsync() => await checks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            Lines.Enqueue((level, text));
            checks.Writer.TryWrite(text);
        }
    }
    private static void WithCertificate(TestClock clock, bool privateKey, bool expired, Action<IdentityOptions> action)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=DocAi-test-" + Guid.NewGuid(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(clock.GetUtcNow().AddDays(-2), clock.GetUtcNow().AddDays(expired ? -1 : 40));
        using var certificate = privateKey ? X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet) : X509CertificateLoader.LoadCertificate(generated.Export(X509ContentType.Cert));
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        var added = false;
        try { store.Open(OpenFlags.ReadWrite); store.Add(certificate); added = true; action(Options(certificate.Thumbprint)); }
        finally { if (added) store.Remove(certificate); DeleteTestKey(certificate); }
    }
    private static void DeleteTestKey(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey) return;
        using var key = certificate.GetRSAPrivateKey();
        if (key is RSACng cng) cng.Key.Delete();
    }
    private static async Task WithCertificateAsync(TestClock clock, Func<IdentityOptions, Task> action)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=DocAi-test-" + Guid.NewGuid(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(clock.GetUtcNow().AddDays(-2), clock.GetUtcNow().AddDays(40));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        var added = false;
        try { store.Open(OpenFlags.ReadWrite); store.Add(certificate); added = true; await action(Options(certificate.Thumbprint)); }
        finally { if (added) store.Remove(certificate); DeleteTestKey(certificate); }
    }
}
