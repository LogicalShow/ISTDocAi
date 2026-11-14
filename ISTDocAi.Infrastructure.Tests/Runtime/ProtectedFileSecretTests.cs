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
using System.Text;
using System.Text.Json;

using ISTDocAi.Infrastructure;

using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ProtectedFileSecretTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "docai-protected-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(folder, "secrets.json");
    private readonly TestClock clock = new();
    private ProtectedFileSecretStore Store() => new(Options.Create(new SecretsOptions
        { ProtectedFile = new() { Path = FilePath } }), clock);
    private static string Key(byte b) => Convert.ToBase64String(Enumerable.Repeat(b, 32).ToArray());
    private void Write(string name, string value, string entropy = "DocAi.WebhookSecrets.v1:LocalMachine", int scope = 1)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Dictionary<string, string>
        { [name] = (scope == 1 ? "dpapi:LocalMachine:v1:" : "dpapi:CurrentUser:v1:") + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(entropy), (DataProtectionScope)scope)) }));
    }
    [Test]
    [Category("Machine")]
    public async Task Round_trip_has_no_plaintext_and_concurrent_readers_receive_independent_keys()
    {
        MachineGate.Require();
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        Write("one", Key(42)); var store = Store();
        PortAssert.DoesNotContain(Key(42), File.ReadAllText(FilePath));
        var keys = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => store.GetSigningKeyAsync("one", default)));
        PortAssert.All(keys, k => PortAssert.Equal(Convert.FromBase64String(Key(42)), k));
        keys[0][0] = 0; PortAssert.Equal(42, keys[1][0]);
    }
    
    [TestCase("wrong-entropy", 1)]
    [TestCase("DocAi.WebhookSecrets.v1:CurrentUser", 0)]
    [Category("Machine")]
    public async Task Wrong_entropy_or_scope_is_a_safe_unavailable_error(string entropy, int scope)
    {
        MachineGate.Require();
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        Write("one", Key(42), entropy, scope);
        var error = await PortAssert.ThrowsAsync<SecretUnavailableException>(() => Store().GetSigningKeyAsync("one", default));
        PortAssert.Equal("Webhook secret unavailable or invalid.", error.Message);
    }
    [Test]
    [Category("Machine")]
    public async Task Missing_file_name_corruption_and_invalid_key_are_unavailable_and_recoverable()
    {
        MachineGate.Require();
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        var store = Store();
        await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("one", default));
        Write("other", Key(42));
        await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("one", default));
        foreach (var value in new[] { "", "not-base64", "YWJj" })
        {
            Write("one", value);
            await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("one", default));
        }
        File.WriteAllText(FilePath, "{broken");
        await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("one", default));
        File.WriteAllText(FilePath, "{\"one\":\"not-ciphertext\"}");
        await PortAssert.ThrowsAsync<SecretUnavailableException>(() => store.GetSigningKeyAsync("one", default));
        Write("one", Key(42)); PortAssert.Equal(Convert.FromBase64String(Key(42)), await store.GetSigningKeyAsync("one", default));
    }
    [Test]
    [Category("Machine")]
    public async Task Cache_expires_after_one_hour_and_invalidation_is_rate_limited_per_name()
    {
        MachineGate.Require();
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows DPAPI is required."); return; }
        Write("one", Key(1)); var store = Store();
        PortAssert.Equal(Convert.FromBase64String(Key(1)), await store.GetSigningKeyAsync("one", default));
        Write("one", Key(2));
        PortAssert.Equal(Convert.FromBase64String(Key(1)), await store.GetSigningKeyAsync("one", default));
        store.InvalidateSigningKey("one");
        PortAssert.Equal(Convert.FromBase64String(Key(2)), await store.GetSigningKeyAsync("one", default));
        Write("one", Key(3)); store.InvalidateSigningKey("one");
        PortAssert.Equal(Convert.FromBase64String(Key(2)), await store.GetSigningKeyAsync("one", default));
        clock.Advance(TimeSpan.FromMinutes(10)); store.InvalidateSigningKey("one");
        PortAssert.Equal(Convert.FromBase64String(Key(3)), await store.GetSigningKeyAsync("one", default));
        Write("one", Key(4)); clock.Advance(TimeSpan.FromHours(1));
        PortAssert.Equal(Convert.FromBase64String(Key(4)), await store.GetSigningKeyAsync("one", default));
    }


    public static IEnumerable<Exception> TransientReadFailures => new Exception[]
    {
        new FileNotFoundException(), new UnauthorizedAccessException(), new JsonException(),
        new IOException("sharing", unchecked((int)0x80070020)),
        new IOException("lock", unchecked((int)0x80070021)),
        new IOException("access", unchecked((int)0x80070005))
    };

    
    [TestCaseSource(nameof(TransientReadFailures))]
    public async Task ProtectedFile_transient_reads_retry_then_succeed(Exception failure)
    {
        var attempts = 0;
        var value = await ProtectedFileSecretStore.ReadEncryptedValueAsync("one", _ =>
        {
            if (++attempts < 3) throw failure;
            return Task.FromResult<Dictionary<string, string>?>(new() { ["one"] = "encrypted" });
        }, default);
        PortAssert.Equal("encrypted", value);
        PortAssert.Equal(3, attempts);
    }

    
    [TestCaseSource(nameof(TransientReadFailures))]
    public async Task ProtectedFile_retry_attempts_are_bounded_and_preserve_final_exception(Exception failure)
    {
        var attempts = 0;
        var actual = await PortAssert.ThrowsAsync(failure.GetType(), () =>
            ProtectedFileSecretStore.ReadEncryptedValueAsync("one", _ =>
            {
                attempts++;
                throw failure;
            }, default));
        PortAssert.Same(failure, actual);
        PortAssert.Equal(6, attempts);
    }

    [Test]
    public async Task ProtectedFile_missing_name_is_not_retried()
    {
        var attempts = 0;
        await PortAssert.ThrowsAsync<SecretUnavailableException>(() =>
            ProtectedFileSecretStore.ReadEncryptedValueAsync("one", _ =>
            {
                attempts++;
                return Task.FromResult<Dictionary<string, string>?>(new() { ["other"] = "encrypted" });
            }, default));
        PortAssert.Equal(1, attempts);
    }

    [Test]
    public async Task ProtectedFile_permanent_errors_are_not_retried()
    {
        foreach (var failure in new Exception[] {
            new DirectoryNotFoundException(), new IOException("unrelated"),
            new FormatException(), new CryptographicException() })
        {
            var attempts = 0;
            var actual = await PortAssert.ThrowsAsync(failure.GetType(), () =>
                ProtectedFileSecretStore.ReadEncryptedValueAsync("one", _ =>
                {
                    attempts++;
                    throw failure;
                }, default));
            PortAssert.Same(failure, actual);
            PortAssert.Equal(1, attempts);
        }
    }

    [Test]
    public async Task ProtectedFile_cancellation_stops_retry_delay()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        await PortAssert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProtectedFileSecretStore.ReadEncryptedValueAsync("one", token =>
            {
                PortAssert.Equal(cancellation.Token, token);
                attempts++;
                cancellation.Cancel();
                throw new FileNotFoundException();
            }, cancellation.Token));
        PortAssert.Equal(1, attempts);
    }
    [TearDown]
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
