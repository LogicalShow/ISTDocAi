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
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;


namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class BlobDocumentMissingTests
{
    
    [TestCase(404, null)] [TestCase(400, "BlobNotFound")] [TestCase(503, null)]
    public async Task Missing_original_is_distinct_and_other_storage_errors_remain_retryable(int status, string? code)
    {
        var store = new BlobDocumentStore(new Container(status, code));
        if (status == 404 || code == "BlobNotFound") await PortAssert.ThrowsAsync<DocumentMissingException>(() => store.OpenReadAsync("private.pdf", default));
        else await PortAssert.ThrowsAsync<RequestFailedException>(() => store.OpenReadAsync("private.pdf", default));
    }
    private sealed class Container(int status, string? code) : BlobContainerClient
    {
        public override BlobClient GetBlobClient(string name) => new Blob(status, code);
    }
    private sealed class Blob(int status, string? code) : BlobClient
    {
        public override Task<Stream> OpenReadAsync(long position = 0, int? bufferSize = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default) =>
            throw new RequestFailedException(status, "Unavailable", code, null);
    }
}
