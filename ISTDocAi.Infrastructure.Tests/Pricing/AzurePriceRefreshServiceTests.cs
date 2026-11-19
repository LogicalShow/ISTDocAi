using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Pricing;
using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Tests.DocAi.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ISTDocAi.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.Pricing;
public sealed class AzurePriceRefreshServiceTests
{
    private sealed class Harness : IAsyncDisposable
    {
        public readonly TestClock Clock = new();
        public readonly DatabaseReadiness Ready = new();
        public readonly List<AzurePriceCheck> Checks = [];
        public int SourceCalls,HistoryCalls,Disposals,Active,MaxActive;
        public bool ThrowStore,ThrowSource;
        public DateTimeOffset? Latest;
        public TaskCompletionSource? Block;
        private readonly ServiceProvider provider;
        public AzurePriceRefreshService Worker { get; }
        private sealed class Source(Harness h) : IAzureRetailPriceSource
        {
            public async Task<IReadOnlyList<AzurePriceObservation>> ReadAsync(CancellationToken ct)
            {
                Interlocked.Increment(ref h.SourceCalls);var active=Interlocked.Increment(ref h.Active);h.MaxActive=Math.Max(h.MaxActive,active);
                try { if(h.Block is not null) await h.Block.Task.WaitAsync(ct);if(h.ThrowSource)throw new InvalidDataException();return [AzurePricePolicyTests.Row(),AzurePricePolicyTests.Row(false)]; }
                finally { Interlocked.Decrement(ref h.Active); }
            }
        }
        private sealed class Store(Harness h) : IAzurePriceCheckStore,IAsyncDisposable
        {
            public Task<DateTimeOffset?> LatestCompletedAsync(CancellationToken ct) { h.HistoryCalls++;return Task.FromResult(h.Latest); }
            public Task<IReadOnlyList<AzurePriceCheckResult>> SaveAsync(AzurePriceCheck check,CancellationToken ct)
            {
                if(h.ThrowStore)throw new InvalidOperationException();h.Checks.Add(check);return Task.FromResult<IReadOnlyList<AzurePriceCheckResult>>([]);
            }
            public ValueTask DisposeAsync() { Interlocked.Increment(ref h.Disposals);return ValueTask.CompletedTask; }
        }
        public Harness(bool enabled=true,TimeSpan? delay=null)
        {
            var services=new ServiceCollection();services.AddSingleton<TimeProvider>(Clock);
            services.AddScoped<IAzureRetailPriceSource>(_=>new Source(this));services.AddScoped<IAzurePriceCheckStore>(_=>new Store(this));services.AddScoped<CheckAzurePrices>();
            provider=services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes=true });
            Worker=new(provider.GetRequiredService<IServiceScopeFactory>(),Options.Create(new AzurePriceRefreshOptions { Enabled=enabled,Interval=TimeSpan.FromHours(1),StartupDelay=delay??TimeSpan.Zero }),Clock,Ready,NullLogger<AzurePriceRefreshService>.Instance);
        }
        public async ValueTask DisposeAsync() { await Worker.StopAsync(default);Worker.Dispose();await provider.DisposeAsync(); }
    }
    [TestCase("00:59:59","00:00:00")][TestCase("01:00:00","-00:00:01")][TestCase("01:00:00","01:00:01")]
    public void Invalid_configuration_is_rejected(string interval,string delay)
    {
        var services=new ServiceCollection();services.AddDocAiWorker(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["AzurePriceRefresh:Interval"]=interval,["AzurePriceRefresh:StartupDelay"]=delay }).Build());
        using var provider=services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(()=>_ = provider.GetRequiredService<IOptions<AzurePriceRefreshOptions>>().Value);
    }
    [Test]
    public void Named_HTTP_client_disables_redirects_cookies_and_credentials()
    {
        var services=new ServiceCollection();services.AddDocAiWorker(new ConfigurationBuilder().Build());
        using var provider=services.BuildServiceProvider();
        var handler=provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("AzureRetailPrices");
        while(handler is DelegatingHandler delegated)handler=delegated.InnerHandler!;
        var http=(HttpClientHandler)handler;
        Assert.That(http.AllowAutoRedirect,Is.False);Assert.That(http.UseCookies,Is.False);
        Assert.That(http.UseDefaultCredentials,Is.False);Assert.That(http.Credentials,Is.Null);
        Assert.That(http.ServerCertificateCustomValidationCallback,Is.Null);Assert.That(http.UseProxy,Is.True);
        Assert.That(provider.GetRequiredService<IOptions<AzurePriceRefreshOptions>>().Value.Enabled,Is.False);
    }
    [Test]
    public async Task Disabled_never_waits_or_opens_a_scope()
    {
        await using var h=new Harness(false);await h.Worker.StartAsync(default);await h.Worker.ExecuteTask!;
        Assert.That(h.HistoryCalls+h.SourceCalls+h.Disposals,Is.Zero);Assert.That(h.Clock.Delays,Is.Empty);
    }
    [Test]
    public async Task Readiness_then_delay_then_run_and_interval_dispose_scopes()
    {
        await using var h=new Harness(delay:TimeSpan.FromMinutes(1));await h.Worker.StartAsync(default);
        Assert.That(h.HistoryCalls,Is.Zero);Assert.That(h.Clock.Delays,Is.Empty);
        h.Ready.MarkReady();await AzureRetailPriceClientTests.Until(()=>h.Clock.Delays.Contains(TimeSpan.FromMinutes(1)));
        h.Clock.Advance(TimeSpan.FromSeconds(59));Assert.That(h.SourceCalls,Is.Zero);
        h.Clock.Advance(TimeSpan.FromSeconds(1));await AzureRetailPriceClientTests.Until(()=>h.Checks.Count==1&&h.Disposals==2);
        h.Clock.Advance(TimeSpan.FromHours(1));await AzureRetailPriceClientTests.Until(()=>h.Checks.Count==2&&h.Disposals==3);
    }
    [Test]
    public async Task Recent_history_skips_first_run_but_not_next_interval()
    {
        await using var h=new Harness();h.Latest=h.Clock.GetUtcNow().AddMinutes(-30);h.Ready.MarkReady();await h.Worker.StartAsync(default);
        await AzureRetailPriceClientTests.Until(()=>h.HistoryCalls==1&&h.Clock.Delays.Contains(TimeSpan.FromHours(1)));
        Assert.That(h.SourceCalls,Is.Zero);h.Clock.Advance(TimeSpan.FromHours(1));await AzureRetailPriceClientTests.Until(()=>h.Checks.Count==1);
    }
    [Test]
    public async Task Long_checks_do_not_overlap_and_shutdown_cancels()
    {
        await using var h=new Harness();h.Block=new(TaskCreationOptions.RunContinuationsAsynchronously);h.Ready.MarkReady();await h.Worker.StartAsync(default);
        await AzureRetailPriceClientTests.Until(()=>h.SourceCalls==1);h.Clock.Advance(TimeSpan.FromHours(3));Assert.That(h.SourceCalls,Is.EqualTo(1));
        await h.Worker.StopAsync(default);Assert.That(h.MaxActive,Is.EqualTo(1));Assert.That(h.Checks,Is.Empty);Assert.That(h.Disposals,Is.EqualTo(2));
    }
    [TestCase(false)][TestCase(true)]
    public async Task Failures_do_not_stop_scheduling(bool source)
    {
        await using var h=new Harness();h.ThrowSource=source;h.ThrowStore=!source;h.Ready.MarkReady();await h.Worker.StartAsync(default);
        await AzureRetailPriceClientTests.Until(()=>h.Disposals==2);
        if(source)Assert.That(h.Checks[0].Outcome,Is.EqualTo("Failed"));
        h.ThrowSource=false;h.ThrowStore=false;h.Clock.Advance(TimeSpan.FromHours(1));await AzureRetailPriceClientTests.Until(()=>h.SourceCalls==2&&h.Disposals==3);
        Assert.That(h.Checks[^1].Outcome,Is.EqualTo("NoChange"));
    }
}
