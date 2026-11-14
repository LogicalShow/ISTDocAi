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
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Services.DocAi.Storage;

public sealed class BlobDocumentStore : IDocumentStore
{
    private readonly BlobContainerClient container;
    public BlobDocumentStore(IOptions<BlobOptions> options, TokenCredential credential)
    {
        var uri = new Uri(options.Value.AccountUrl);
        if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("A keyless HTTPS Blob account URL is required.");
        container = new BlobServiceClient(uri, credential).GetBlobContainerClient(options.Value.Container);
    }
    internal BlobDocumentStore(BlobContainerClient container) => this.container = container;
    public async Task PutAsync(string path, Stream content, string contentType, CancellationToken ct) =>
        _ = await container.GetBlobClient(path).UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
    public async Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct)
    {
        try { return await container.GetBlobClient(blobPath).OpenReadAsync(cancellationToken: ct); }
        catch (RequestFailedException e) when (e.Status == 404 || e.ErrorCode == "BlobNotFound") { throw new DocumentMissingException(); }
    }
    public async Task CheckAsync(CancellationToken ct)
    {
        var properties = await container.GetPropertiesAsync(cancellationToken: ct);
        if (properties.Value.PublicAccess != PublicAccessType.None) throw new InvalidOperationException("Blob container must be private.");
    }
}
