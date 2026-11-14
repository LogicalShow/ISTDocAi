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
public sealed class BlobRawResultStoreTests
{
    private sealed class Container : BlobContainerClient
    {
        public int Checks;
        public int Uploads;
        public int Reads;
        public int Deletes;
        public bool Public;
        public bool Fail;
        public List<string> Names { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Block;
        public override async Task<Response<BlobContainerProperties>> GetPropertiesAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Checks); Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(cancellationToken);
            if (Fail) throw new RequestFailedException(503, "Unavailable");
            return Response.FromValue(BlobsModelFactory.BlobContainerProperties(default, default, (LeaseStatus?)null, (LeaseState?)null, (LeaseDurationType?)null, Public ? PublicAccessType.Blob : PublicAccessType.None, null, null, new Dictionary<string, string>()), null!);
        }
        public override BlobClient GetBlobClient(string blobName)
        {
            lock (Names) Names.Add(blobName);
            return new Blob(this);
        }
        private sealed class Blob(Container owner) : BlobClient
        {
            public override Task<Response<BlobContentInfo>> UploadAsync(BinaryData content, BlobUploadOptions options, CancellationToken cancellationToken = default)
            {
                PortAssert.Equal("application/json", options.HttpHeaders.ContentType);
                Interlocked.Increment(ref owner.Uploads);
                return Task.FromResult(Response.FromValue<BlobContentInfo>(null!, null!));
            }
            public override Task<Response<bool>> DeleteIfExistsAsync(DeleteSnapshotsOption snapshotsOption = default,
                BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.Deletes);
                return Task.FromResult(Response.FromValue(true, null!));
            }
            public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.Reads);
                return Task.FromResult(Response.FromValue(BlobsModelFactory.BlobDownloadResult(BinaryData.FromString("{}")), null!));
            }
        }
    }
    [Test]
    public async Task Stored_path_includes_container_but_blob_name_does_not()
    {
        var container = new Container(); using var store = new BlobRawResultStore(container);
        var job = Guid.NewGuid();
        var path = await store.SaveAsync(job, 2, 123, "{}", default);
        PortAssert.Equal($"results/{job:D}/2/123.json", path);
        PortAssert.Equal("{}", await store.ReadAsync(path, default));
        await store.DeleteAsync(path, default);
        PortAssert.Equal(1, container.Deletes);
        PortAssert.Equal(new[] { $"{job:D}/2/123.json", $"{job:D}/2/123.json", $"{job:D}/2/123.json" }, container.Names);
    }
    
    [TestCase(null)] [TestCase("")] [TestCase("results/a/1/1.json")]
    [TestCase("documents/11111111-1111-1111-1111-111111111111/1/1.json")]
    [TestCase("results/00000000-0000-0000-0000-000000000000/1/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/0/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/-1/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/0.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/-1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/32768/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/9223372036854775808.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/1.JSON")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/1.json?x=1")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/1.json/extra")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/../1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/ 1/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111/1/+1.json")]
    [TestCase("results/ 11111111-1111-1111-1111-111111111111/1/1.json")]
    [TestCase("results/11111111-1111-1111-1111-111111111111 /1/1.json")]
    public async Task Invalid_stored_paths_never_reach_blob_client(string? path)
    {
        var container = new Container(); using var store = new BlobRawResultStore(container);
        await PortAssert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(path!, default));
        await PortAssert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(path!, default));
        PortAssert.Empty(container.Names); PortAssert.Equal(0, container.Reads); PortAssert.Equal(0, container.Deletes);
    }
    [Test]
    public async Task Concurrent_first_saves_check_privacy_once_and_later_saves_reuse_success()
    {
        var container = new Container { Block = true }; using var store = new BlobRawResultStore(container);
        var job = Guid.NewGuid();
        var saves = Enumerable.Range(1, 10).Select(i => store.SaveAsync(job, 1, i, "{}", default)).ToArray();
        await container.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        PortAssert.Equal(1, container.Checks); PortAssert.Equal(0, container.Uploads);
        container.Release.SetResult(); await Task.WhenAll(saves);
        await store.SaveAsync(job, 1, 11, "{}", default);
        PortAssert.Equal(1, container.Checks); PortAssert.Equal(11, container.Uploads);
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Failed_or_public_checks_are_not_cached_and_never_upload(bool isPublic)
    {
        var container = new Container { Public = isPublic, Fail = !isPublic }; using var store = new BlobRawResultStore(container);
        var job = Guid.NewGuid();
        if (isPublic) await PortAssert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(job, 1, 1, "{}", default));
        else await PortAssert.ThrowsAsync<RequestFailedException>(() => store.SaveAsync(job, 1, 1, "{}", default));
        PortAssert.Equal(0, container.Uploads);
        container.Public = false; container.Fail = false;
        await store.SaveAsync(job, 1, 1, "{}", default);
        await store.SaveAsync(job, 1, 2, "{}", default);
        PortAssert.Equal(2, container.Checks); PortAssert.Equal(2, container.Uploads);
    }
}


