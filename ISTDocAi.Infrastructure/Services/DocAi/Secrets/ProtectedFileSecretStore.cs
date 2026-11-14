using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
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
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Secrets;

public sealed class ProtectedFileSecretStore(IOptions<SecretsOptions> options, TimeProvider clock) : CachedSecretStore(clock)
{
    // Format contract shared with deploy/_webhook-secrets.ps1. Not a secret.
    // LocalMachine supports admin provisioning for IIS identities without loaded profiles.
    // NTFS ACLs are the access boundary; DPAPI binds ciphertext to this Windows machine.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DocAi.WebhookSecrets.v1:LocalMachine");

    protected override async Task<string> ReadValueAsync(string name, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ProtectedFile secrets require Windows DPAPI.");
        var encrypted = await ReadEncryptedValueAsync(name, async token =>
        {
            // Allow atomic replacement while readers hold the previous file open.
            await using var stream = new FileStream(options.Value.ProtectedFile.Path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: token);
        }, ct);
        // DPAPI unprotect follows the blob's native scope, not its scope argument.
        // Explicit, scope-bound format prevents accepting a different provider/scope format.
        const string prefix = "dpapi:LocalMachine:v1:";
        if (!encrypted.StartsWith(prefix, StringComparison.Ordinal)) throw new SecretUnavailableException(name);
        var clear = ProtectedData.Unprotect(Convert.FromBase64String(encrypted[prefix.Length..]), Entropy, DataProtectionScope.LocalMachine);
        try { return new UTF8Encoding(false, true).GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    // The delegate is only file open/read/parse: lookup and DPAPI errors must not be retried.
    internal static async Task<string> ReadEncryptedValueAsync(string name,
        Func<CancellationToken, Task<Dictionary<string, string>?>> readFile, CancellationToken ct)
    {
        Dictionary<string, string>? values;
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                values = await readFile(ct);
                break;
            }
            catch (Exception error) when (attempt < 5 && IsTransientReadFailure(error))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 << attempt), ct);
            }
        }
        if (values is null || !values.TryGetValue(name, out var encrypted)) throw new SecretUnavailableException(name);
        return encrypted;
    }

    private static bool IsTransientReadFailure(Exception error) => error is
        FileNotFoundException or UnauthorizedAccessException or JsonException ||
        error is IOException && error.HResult is unchecked((int)0x80070020) or
            unchecked((int)0x80070021) or unchecked((int)0x80070005);
}
