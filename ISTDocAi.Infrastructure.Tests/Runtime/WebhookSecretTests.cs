using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Services.DocAi.Webhooks;
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
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Security.KeyVault.Secrets;

using ISTDocAi.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class WebhookSecretTests
{
    private sealed class Vault : SecretClient
    {
        public int Reads;
        public string Value = Convert.ToBase64String(new byte[32]);
        public Exception? Error;
        public Func<string, string>? ValueFor;
        public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            Reads++; if (Error is not null) throw Error;
            return Task.FromResult(Response.FromValue(new KeyVaultSecret(name, ValueFor?.Invoke(name) ?? Value), null!));
        }
    }



    [Test]
    public async Task Private_key_and_non_certificate_blocks_are_refused_and_public_CA_is_cached()
    {
        using var ca = WebhookTransportTests.CreateCa("Root");
        var vault = new Vault { Value = ca.ExportCertificatePem() }; var clock = new TestClock();
        var store = new KeyVaultSecretStore(vault, clock);
        PortAssert.Equal(vault.Value, await store.GetCaCertificatePemAsync("ca", default));
        _ = await store.GetCaCertificatePemAsync("ca", default); PortAssert.Equal(1, vault.Reads);
        foreach (var pem in new[] { ca.ExportCertificatePem() + "\n" + ca.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem(),
            "-----BEGIN PRIVATE KEY-----\nAQID\n-----END PRIVATE KEY-----", ca.ExportCertificatePem() + "junk" })
        {
            vault.Value = pem;
            await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetCaCertificatePemAsync(Guid.NewGuid().ToString(), default));
        }
    }

    [Test]
    public async Task Readiness_uses_cached_signing_key_without_extra_Vault_calls()
    {
        var f = new WebhookFakes(); var vault = new Vault(); var store = new KeyVaultSecretStore(vault, f.Clock);
        var readiness = new WebhookReadiness(f, store, f.FailureLog);
        PortAssert.True(await readiness.CheckAsync(default)); PortAssert.True(await readiness.CheckAsync(default)); PortAssert.Equal(1, vault.Reads);
        vault.Error = new IOException("offline");
        PortAssert.True(await readiness.CheckAsync(default));
        f.Clock.Advance(TimeSpan.FromHours(1)); PortAssert.False(await readiness.CheckAsync(default));
    }
    [Test]
    public async Task Readiness_prefers_the_last_readable_secret_so_a_broken_neighbor_does_not_trigger_extra_Vault_calls()
    {
        var f = new WebhookFakes();
        var vault = new Vault { ValueFor = name => name == "broken" ? throw new RequestFailedException(403, "denied") : Convert.ToBase64String(new byte[32]) };
        var store = new KeyVaultSecretStore(vault, f.Clock);
        var readiness = new WebhookReadiness(new TwoCallbacks(), store, f.FailureLog);
        PortAssert.True(await readiness.CheckAsync(default)); PortAssert.Equal(2, vault.Reads);
        PortAssert.True(await readiness.CheckAsync(default)); PortAssert.Equal(2, vault.Reads);
    }
    private sealed class TwoCallbacks : ICallbackDirectory
    {
        public Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CallbackConsumer>>([
            new(1, "ONE", "https://one.example", "broken", null), new(2, "TWO", "https://two.example", "good", null)]);
    }
    
    [TestCase("https://docai.vault.azure.net", true)] [TestCase("http://docai.vault.azure.net", false)]
    [TestCase("https://vault.azure.net", false)] [TestCase("https://evil.example", false)]
    [TestCase("https://docai.vault.azure.net.evil.example", false)] [TestCase("https://user@docai.vault.azure.net", false)]
    public void Vault_url_must_be_an_https_Azure_vault(string url, bool valid) => PortAssert.Equal(valid, KeyVaultOptions.IsValid(new() { Url = url }));
    [Test]
    public async Task Callback_directory_refreshes_at_sixty_seconds_and_observes_disabled_consumers()
    {
        var source = new Source(); var clock = new TestClock();
        var services = new ServiceCollection(); services.AddScoped<ICallbackSource>(_ => source);
        using var provider = services.BuildServiceProvider(); var directory = new CallbackDirectory(provider.GetRequiredService<IServiceScopeFactory>(), clock);
        PortAssert.Single(await directory.ReadAsync(default)); PortAssert.Single(await directory.ReadAsync(default)); PortAssert.Equal(1, source.Reads);
        source.Enabled = false; clock.Advance(TimeSpan.FromSeconds(59)); PortAssert.Single(await directory.ReadAsync(default));
        clock.Advance(TimeSpan.FromSeconds(1)); PortAssert.Empty(await directory.ReadAsync(default)); PortAssert.Equal(2, source.Reads);
    }
    private sealed class Source : ICallbackSource
    {
        public int Reads; public bool Enabled = true;
        public Task<IReadOnlyList<CallbackConsumer>> GetCallbacksAsync(CancellationToken ct)
        { Reads++; return Task.FromResult<IReadOnlyList<CallbackConsumer>>(Enabled ? [new(1, "TEST", "https://test.example", "secret", null)] : []); }
    }

    
    [TestCase(403)] [TestCase(404)] [TestCase(503)]
    public async Task Vault_errors_do_not_send_or_record_and_are_critical_once_per_minute(int status)
    {
        var vault = new Vault { Error = new RequestFailedException(status, "PRIVATE_SECRET_TOKEN") };
        var f = new WebhookFakes(); var store = new KeyVaultSecretStore(vault, f.Clock);
        var processor = new DeliverWebhook(f, f, store, f, f.Clock, f.FailureLog, f.Logs, new ISTDocAi.Application.Services.DocAi.WebhookPolicyService.WebhookPolicyService(), new DocAiV2Json());
        await processor.ExecuteAsync(f.Delivery); await processor.ExecuteAsync(f.Delivery);
        Assert.Empty(f.Attempts); Assert.Empty(f.Requests); Assert.Single(f.SecretLogs.Lines);
        Assert.DoesNotContain("PRIVATE_SECRET_TOKEN", f.SecretLogs.Lines.First().Text);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await processor.ExecuteAsync(f.Delivery); Assert.Equal(2, f.SecretLogs.Lines.Count);
    }
    
    [TestCase("")] [TestCase("not base64")] [TestCase("YWJj")]
    public async Task Invalid_signing_secret_is_a_configuration_error_without_an_attempt(string value)
    {
        var f = new WebhookFakes(); var store = new KeyVaultSecretStore(new Vault { Value = value }, f.Clock);
        await Assert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("secret", default));
        await new DeliverWebhook(f, f, store, f, f.Clock, f.FailureLog, f.Logs, new ISTDocAi.Application.Services.DocAi.WebhookPolicyService.WebhookPolicyService(), new DocAiV2Json()).ExecuteAsync(f.Delivery);
        Assert.Empty(f.Attempts); Assert.Empty(f.Requests); Assert.Single(f.SecretLogs.Lines);
    }
    [Test]
    public async Task CA_with_a_private_key_is_critical_and_does_not_send_or_spend_an_attempt()
    {
        using var ca = WebhookTransportTests.CreateCa("Root");
        using var privateKey = ca.GetRSAPrivateKey()!;
        var pem = ca.ExportCertificatePem() + "\n" + privateKey.ExportPkcs8PrivateKeyPem();
        var vault = new Vault { ValueFor = name => name == "ca" ? pem : Convert.ToBase64String(new byte[32]) };
        var f = new WebhookFakes(); f.Callback = f.Callback! with { CallbackCaCertName = "ca" };
        var store = new KeyVaultSecretStore(vault, f.Clock);
        await new DeliverWebhook(f, f, store, f, f.Clock, f.FailureLog, f.Logs, new ISTDocAi.Application.Services.DocAi.WebhookPolicyService.WebhookPolicyService(), new DocAiV2Json()).ExecuteAsync(f.Delivery);
        Assert.Empty(f.Requests); Assert.Empty(f.Attempts); Assert.Single(f.SecretLogs.Lines);
    }
    [Test]
    public async Task Secret_cache_is_one_hour_and_401_refreshes_next_attempt_at_most_every_ten_minutes()
    {
        var vault = new Vault(); var f = new WebhookFakes { Response = new(401) };
        var store = new KeyVaultSecretStore(vault, f.Clock);
        var processor = new DeliverWebhook(f, f, store, f, f.Clock, f.FailureLog, f.Logs, new ISTDocAi.Application.Services.DocAi.WebhookPolicyService.WebhookPolicyService(), new DocAiV2Json());
        await processor.ExecuteAsync(f.Delivery); Assert.Equal(1, vault.Reads);
        vault.Value = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());
        await processor.ExecuteAsync(f.Delivery); Assert.Equal(2, vault.Reads);
        Assert.NotEqual(f.Requests[0].Signature, f.Requests[1].Signature);
        await processor.ExecuteAsync(f.Delivery); Assert.Equal(2, vault.Reads);
        f.Clock.Advance(TimeSpan.FromMinutes(10));
        await processor.ExecuteAsync(f.Delivery); Assert.Equal(2, vault.Reads);
        await processor.ExecuteAsync(f.Delivery); Assert.Equal(3, vault.Reads);
        Assert.Equal(5, f.Attempts.Count);
        f.Clock.Advance(TimeSpan.FromHours(1));
        _ = await store.GetSigningKeyAsync("secret", default); Assert.Equal(4, vault.Reads);
    }
}
