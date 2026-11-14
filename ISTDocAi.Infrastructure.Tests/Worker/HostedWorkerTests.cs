using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Common.Mappings.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ErrorCodes = ISTDocAi.Domain.Common.Constants.DocAi.ErrorCodes;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class HostedWorkerTests
{
    [Test]
    public async Task Claim_admission_rechecks_breaker_after_slot_observed_closed()
    {
        var clock = new TestClock(); var breaker = new WorkerCircuitBreaker(clock, new TestLogger<WorkerCircuitBreaker>());
        var queue = new LoopQueue();
        Assert.Equal(TimeSpan.Zero, breaker.Remaining);
        breaker.Open();
        Assert.Null(await breaker.ClaimAsync(queue, default)); Assert.Equal(0, queue.Claims);
        clock.Advance(TimeSpan.FromMinutes(5));
        await breaker.ClaimAsync(queue, default); Assert.Equal(1, queue.Claims);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breaker.ClaimAsync(queue, stop.Token)); Assert.Equal(1, queue.Claims);
    }
    [Test]
    public async Task Breaker_blocks_all_claims_for_five_minutes_and_logs_once_per_opening()
    {
        var clock = new TestClock(); var logs = new TestLogger<WorkerCircuitBreaker>();
        var breaker = new WorkerCircuitBreaker(clock, logs); breaker.Open(); breaker.Open();
        var queue = new LoopQueue(); using var services = Services(queue, clock, breaker);
        using var worker = Worker(services, clock, breaker, 4);
        await worker.StartAsync(default); await Settle(); Assert.Equal(0, queue.Claims);
        clock.Advance(TimeSpan.FromSeconds(299)); await Settle(); Assert.Equal(0, queue.Claims);
        clock.Advance(TimeSpan.FromSeconds(1)); await Until(() => queue.Claims == 4);
        Assert.Single(logs.Lines, l => l.Level == LogLevel.Critical && l.Text.Contains("breaker.opened"));
        breaker.Open(); Assert.Equal(2, logs.Lines.Count(l => l.Level == LogLevel.Critical));
        await worker.StopAsync(default);
    }
    [Test]
    public async Task Idle_delay_doubles_to_thirty_seconds_and_shutdown_cancels_it_without_another_claim()
    {
        var clock = new TestClock(); var breaker = new WorkerFakes(); var queue = new LoopQueue();
        using var services = Services(queue, clock, breaker); using var worker = Worker(services, clock, breaker, 1);
        await worker.StartAsync(default);
        var expected = new[] { 2, 4, 8, 16, 30, 30 };
        for (var i = 0; i < expected.Length; i++)
        {
            await Until(() => clock.Delays.Count > i);
            Assert.Equal(TimeSpan.FromSeconds(expected[i]), clock.Delays[i]);
            if (i < expected.Length - 1) clock.Advance(TimeSpan.FromSeconds(expected[i]));
        }
        var count = queue.Claims; await worker.StopAsync(default);
        clock.Advance(TimeSpan.FromHours(1)); await Settle(); Assert.Equal(count, queue.Claims);
    }
    [Test]
    public async Task Parallelism_is_bounded_and_each_slot_claims_immediately_after_its_job()
    {
        var clock = new TestClock(); var breaker = new WorkerFakes(); var queue = new LoopQueue { Supply = true };
        var analyzer = new BlockingAnalyzer(); using var services = Services(queue, clock, breaker, analyzer);
        using var worker = Worker(services, clock, breaker, 4); await worker.StartAsync(default);
        await Until(() => analyzer.Active == 4); Assert.Equal(4, queue.Claims); Assert.Equal(4, analyzer.Maximum);
        analyzer.AllowOne(); await Until(() => queue.Claims == 5); Assert.Equal(4, analyzer.Maximum);
        await worker.StopAsync(default); Assert.Equal(0, analyzer.Active);
        Assert.Equal(4, queue.Releases); Assert.Equal(0, queue.BadReleases);
    }
    [Test]
    public async Task Successful_claim_resets_idle_backoff_to_two_seconds()
    {
        var clock = new TestClock(); var breaker = new WorkerFakes(); var queue = new LoopQueue();
        using var services = Services(queue, clock, breaker); using var worker = Worker(services, clock, breaker, 1);
        await worker.StartAsync(default); await Until(() => clock.Delays.Count == 1);
        clock.Advance(TimeSpan.FromSeconds(2)); await Until(() => clock.Delays.Count == 2);
        queue.Once = new WorkerFakes().Job with { TakeoverCount = 3 };
        clock.Advance(TimeSpan.FromSeconds(4)); await Until(() => clock.Delays.Count == 4);
        Assert.Equal(TimeSpan.FromSeconds(15), clock.Delays[2]); // owned-job heartbeat
        Assert.Equal(TimeSpan.FromSeconds(2), clock.Delays[3]); await worker.StopAsync(default);
    }
    [Test]
    public async Task Claim_error_is_isolated_and_slot_continues()
    {
        var clock = new TestClock(); var breaker = new WorkerFakes(); var queue = new LoopQueue { ThrowOnce = true };
        using var services = Services(queue, clock, breaker); using var worker = Worker(services, clock, breaker, 1);
        await worker.StartAsync(default); await Until(() => clock.Delays.Count == 1);
        clock.Advance(TimeSpan.FromSeconds(2)); await Until(() => queue.Claims == 2); await worker.StopAsync(default);
    }
    [Test]
    public async Task Disabled_worker_never_claims_and_registration_validates_options_and_shutdown_timeout()
    {
        var clock = new TestClock(); var breaker = new WorkerFakes(); var queue = new LoopQueue();
        using var services = Services(queue, clock, breaker); using var worker = new JobProcessorService(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerOptions { Enabled = false }), breaker, clock, new TestLogger<JobProcessorService>(), ReadyDatabase.Instance);
        await worker.StartAsync(default); await worker.StopAsync(default); Assert.Equal(0, queue.Claims);
        var collection = new ServiceCollection(); collection.AddLogging();
        collection.AddDocAiWorker(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Worker:MaxParallelJobs"] = "0" }).Build());
        using var provider = collection.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WorkerOptions>>().Value);
        Assert.Equal(TimeSpan.FromSeconds(210), provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        Assert.Equal(4, new WorkerOptions().MaxParallelJobs); Assert.True(new WorkerOptions().Enabled);
    }
    
    [TestCase(-1, false)] [TestCase(0, false)] [TestCase(1, true)] [TestCase(4, true)]
    [TestCase(16, true)] [TestCase(17, false)] [TestCase(400, false)]
    public void Parallel_job_options_accept_only_one_through_sixteen(int slots, bool valid)
    {
        var collection = new ServiceCollection(); collection.AddLogging();
        collection.AddDocAiWorker(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Worker:MaxParallelJobs"] = slots.ToString() }).Build());
        using var provider = collection.BuildServiceProvider();
        if (valid) Assert.Equal(slots, provider.GetRequiredService<IOptions<WorkerOptions>>().Value.MaxParallelJobs);
        else Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WorkerOptions>>().Value);
    }
    private static JobProcessorService Worker(ServiceProvider services, TestClock clock, IWorkerCircuitBreaker breaker, int slots) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new WorkerOptions { MaxParallelJobs = slots }), breaker, clock, new TestLogger<JobProcessorService>(), ReadyDatabase.Instance);
    private static ServiceProvider Services(LoopQueue queue, TestClock clock, IWorkerCircuitBreaker breaker, IDocumentAnalyzer? analyzer = null)
    {
        var c = new ServiceCollection();
        ISTDocAi.Application.DependencyInjection.AddApplication(c);
        c.AddScoped<WorkerFakes>(_ => { var f = new WorkerFakes(); f.Accepted(); return f; });
        c.AddSingleton<IJobQueue>(queue);
        c.AddScoped<IJobRepository>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IOperationStore>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<ICatalogReader>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IModelThresholdReader>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IDocumentStore>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IDocumentAnalyzer>(s => analyzer ?? s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IRawResultStore>(s => s.GetRequiredService<WorkerFakes>());
        c.AddScoped<IAnalysisParser>(s => s.GetRequiredService<WorkerFakes>());
        c.AddSingleton<IWorkerCircuitBreaker>(breaker); c.AddSingleton<TimeProvider>(clock); c.AddLogging(); c.AddScoped<ProcessJob>();
        return c.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static async Task Settle() => await Task.Delay(30);
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
    private sealed class LoopQueue : IJobQueue
    {
        private int claims, releases, badReleases;
        public int Claims => Volatile.Read(ref claims);
        public int Releases => Volatile.Read(ref releases);
        public int BadReleases => Volatile.Read(ref badReleases);
        public bool Supply { get; init; }
        public bool ThrowOnce { get; set; }
        public ClaimedJob? Once { get; set; }
        public Task<ClaimedJob?> ClaimAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref claims);
            if (ThrowOnce) { ThrowOnce = false; throw new IOException("PRIVATE_TOKEN"); }
            var result = Once; Once = null;
            return Task.FromResult(result ?? (Supply ? new WorkerFakes().Job : null));
        }
        public Task<WriteOutcome> ReleaseAsync(Lease lease, int delay, string? error, CancellationToken ct)
        {
            Interlocked.Increment(ref releases); if (delay != 0 || error is not null) Interlocked.Increment(ref badReleases);
            return Task.FromResult(WriteOutcome.Applied);
        }
        public Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken ct) => Task.FromResult(WriteOutcome.Applied);
        public Task<WriteOutcome> RetryAsync(Lease lease, int delay, string? error, CancellationToken ct) => Task.FromResult(WriteOutcome.Applied);
    }
    private sealed class BlockingAnalyzer : IDocumentAnalyzer
    {
        private int active, maximum;
        private readonly SemaphoreSlim replies = new(0);
        public int Active => Volatile.Read(ref active);
        public int Maximum => Volatile.Read(ref maximum);
        public void AllowOne() => replies.Release();
        public async Task<PollResult> PollAsync(string location, CancellationToken ct)
        {
            var count = Interlocked.Increment(ref active);
            int observed;
            do { observed = maximum; if (observed >= count) break; } while (Interlocked.CompareExchange(ref maximum, count, observed) != observed);
            try { await replies.WaitAsync(ct); return new PollResult.NotFound(); }
            finally { Interlocked.Decrement(ref active); }
        }
    public Task<bool> CheckAccessAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<SubmitPermit> ReserveSubmitAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<SubmitResult> SubmitAsync(SubmitPermit permit, ISTDocAi.Domain.Common.Enums.DocAi.OperationKind kind, string model, ReadOnlyMemory<byte> bytes, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteResultAsync(string location, CancellationToken ct) => Task.CompletedTask;
    }
}
