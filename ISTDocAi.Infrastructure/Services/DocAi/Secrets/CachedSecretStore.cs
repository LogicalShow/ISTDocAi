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
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace ISTDocAi.Infrastructure.Services.DocAi.Secrets;

public abstract class CachedSecretStore(TimeProvider clock) : ISecretStore
{
    protected abstract Task<string> ReadValueAsync(string name, CancellationToken ct);
    private sealed class Entry
    {
        public readonly SemaphoreSlim Fetch = new(1);
        public string? Value;
        public DateTimeOffset Expires, NextInvalidation;
        public int Generation;
    }
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private async Task<string> ReadAsync(string name, CancellationToken ct)
    {
        var entry = entries.GetOrAdd(name, _ => new());
        await entry.Fetch.WaitAsync(ct);
        try
        {
            int generation;
            lock (entry)
            {
                if (entry.Value is not null && entry.Expires > clock.GetUtcNow()) return entry.Value;
                generation = entry.Generation;
            }
            string value;
            try { value = await ReadValueAsync(name, ct); }
            catch (Exception) { throw new SecretUnavailableException(name); }
            lock (entry)
            {
                if (generation == entry.Generation) { entry.Value = value; entry.Expires = clock.GetUtcNow().AddHours(1); }
            }
            return value;
        }
        finally { entry.Fetch.Release(); }
    }
    public async Task<byte[]> GetSigningKeyAsync(string name, CancellationToken ct)
    {
        try
        {
            var key = Convert.FromBase64String(await ReadAsync(name, ct));
            if (key.Length < 32) throw new FormatException();
            return key;
        }
        catch (SecretUnavailableException) { throw; }
        catch (Exception) { ForgetInvalid(name); throw new SecretUnavailableException(name); }
    }
    public async Task<string> GetCaCertificatePemAsync(string name, CancellationToken ct)
    {
        try
        {
            var pem = await ReadAsync(name, ct);
            using var certificate = PrivateCa.Parse(pem);
            return pem;
        }
        catch (SecretUnavailableException) { throw; }
        catch (Exception) { ForgetInvalid(name); throw new SecretUnavailableException(name); }
    }
    private void ForgetInvalid(string name)
    {
        if (entries.TryGetValue(name, out var entry)) lock (entry) { entry.Value = null; entry.Generation++; }
    }
    public void InvalidateSigningKey(string name)
    {
        var entry = entries.GetOrAdd(name, _ => new());
        lock (entry)
        {
            var now = clock.GetUtcNow();
            if (entry.NextInvalidation > now) return;
            entry.NextInvalidation = now.AddMinutes(10);
            entry.Value = null; entry.Generation++;
        }
    }
}
