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
using System.Globalization;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Storage;

public sealed class BlobRawResultStore : IRawResultStore, IDisposable
{
    private readonly BlobContainerClient container;
    private readonly SemaphoreSlim privacyGate = new(1, 1);
    private bool privacyChecked;
    public BlobRawResultStore(IOptions<BlobOptions> options, TokenCredential credential)
    {
        var uri = new Uri(options.Value.AccountUrl);
        if (uri.Scheme != "https" || uri.Query.Length != 0 || uri.UserInfo.Length != 0)
            throw new ArgumentException("A keyless HTTPS Blob account URL is required.");
        container = new BlobServiceClient(uri, credential).GetBlobContainerClient("results");
    }
    internal BlobRawResultStore(BlobContainerClient container) => this.container = container;
    public static string PathFor(Guid jobId, short runNo, long operationId)
    {
        if (jobId == Guid.Empty || operationId < 1 || runNo < 1) throw new ArgumentException("Valid job, run and operation identifiers are required.");
        return FormattableString.Invariant($"results/{jobId:D}/{runNo}/{operationId}.json");
    }
    private static string BlobName(string storedPath)
    {
        var parts = storedPath?.Split('/');
        if (parts is not { Length: 4 } || parts[0] != "results" || parts[1].Length != 36 ||
            !Guid.TryParseExact(parts[1], "D", out var job) || job == Guid.Empty ||
            !short.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var run) || run < 1 ||
            !parts[3].EndsWith(".json", StringComparison.Ordinal) ||
            !long.TryParse(parts[3].AsSpan(0, parts[3].Length - 5), NumberStyles.None, CultureInfo.InvariantCulture, out var operation) || operation < 1)
            throw new ArgumentException("A valid results job/run/operation path is required.", nameof(storedPath));
        return storedPath!["results/".Length..];
    }
    private async Task EnsurePrivateAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref privacyChecked)) return;
        await privacyGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref privacyChecked)) return;
            // Singleton lifetime: only successful checks are cached; transient/public failures may be checked again.
            var properties = await container.GetPropertiesAsync(cancellationToken: ct);
            if (properties.Value.PublicAccess != PublicAccessType.None) throw new InvalidOperationException("Results container must be private.");
            Volatile.Write(ref privacyChecked, true);
        }
        finally { privacyGate.Release(); }
    }
    public async Task<string> SaveAsync(Guid jobId, short runNo, long operationId, string rawJson, CancellationToken ct)
    {
        var path = PathFor(jobId, runNo, operationId);
        await EnsurePrivateAsync(ct);
        await container.GetBlobClient(BlobName(path)).UploadAsync(BinaryData.FromString(rawJson), new BlobUploadOptions
        { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } }, ct);
        return path;
    }
    public async Task<string> ReadAsync(string blobPath, CancellationToken ct)
    {
        var result = await container.GetBlobClient(BlobName(blobPath)).DownloadContentAsync(ct);
        return result.Value.Content.ToString();
    }
    public async Task DeleteAsync(string path, CancellationToken ct) =>
        _ = await container.GetBlobClient(BlobName(path)).DeleteIfExistsAsync(cancellationToken: ct);
    public void Dispose() => privacyGate.Dispose();
}
