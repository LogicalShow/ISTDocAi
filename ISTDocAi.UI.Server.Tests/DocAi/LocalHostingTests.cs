using System.Net;
using Azure.Core;
using Azure.Identity;
using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Microsoft.AspNetCore.TestHost;

using ISTDocAi.Infrastructure;

using TestClock = ISTDocAi.Infrastructure.Tests.DocAi.Worker.TestClock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class LocalHostingTests
{
    [Test]
    public async Task Development_starts_API_worker_retention_and_signals_without_telemetry_or_external_calls()
    {
        using var factory = new LocalFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        await factory.Queue.Claimed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var services = factory.Services;
        Assert.IsType<DefaultAzureCredential>(services.GetRequiredService<TokenCredential>());
        Assert.Null(services.GetService<TracerProvider>());
        Assert.True(services.GetRequiredService<IOptions<WorkerOptions>>().Value.Enabled);
        Assert.True(services.GetRequiredService<IOptions<RetentionOptions>>().Value.Enabled);
        Assert.True(services.GetRequiredService<IOptions<SignalsOptions>>().Value.Enabled);
        foreach (var type in new[] { typeof(JobProcessorService), typeof(RetentionService), typeof(SignalsService) })
        {
            var service = Assert.Single(services.GetServices<IHostedService>(), s => s.GetType() == type);
            Assert.False(((BackgroundService)service).ExecuteTask!.IsCompleted);
        }
    }
    private sealed class LocalFactory() : BackendFactory(false)
    {
        private static IDatabaseReadiness TestDatabaseGate { get; } = ReadyGate();
        private static DatabaseReadiness ReadyGate() { var gate = new DatabaseReadiness(); gate.MarkReady(); return gate; }
        public EmptyQueue Queue { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["DocAi:Mapping:Source"] = "Sql",
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = " ", ["DocAi:Telemetry:UseEntra"] = "false",
                ["DocAi:Identity:Mode"] = "ManagedIdentity",
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid.invalid;Database=BackendTestsNeverConnect;Integrated Security=True;TrustServerCertificate=True",
                ["DocAi:Blob:AccountUrl"] = "https://docailocaltest.blob.core.windows.net/", ["DocAi:Blob:Container"] = "documents",
                ["DocAi:DocumentIntelligence:Endpoint"] = "https://docailocaltest.cognitiveservices.azure.com/",
                ["DocAi:KeyVault:Url"] = "https://docailocaltest.vault.azure.net/",
                ["Worker:Enabled"] = "true", ["Worker:MaxParallelJobs"] = "4",
                ["Retention:Enabled"] = "true", ["Retention:RunAtUtc"] = "02:00:00",
                ["AzurePriceRefresh:Enabled"] = "false", ["Signals:Enabled"] = "true", ["Webhooks:Enabled"] = "false"
            }));
            builder.ConfigureTestServices(services => {
                services.AddHostedService<JobProcessorService>();
                services.AddHostedService<RetentionService>();
                services.AddHostedService<SignalsService>();
                services.RemoveAll<IDatabaseReadiness>();
                services.AddSingleton<IDatabaseReadiness>(TestDatabaseGate);
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new TestClock());
                services.RemoveAll<IJobQueue>(); services.AddSingleton<IJobQueue>(Queue);
            });
        }
    }
    // No jobs or timer ticks: SQL/Azure adapters can be constructed, but cannot make network calls.
    private sealed class EmptyQueue : IJobQueue
    {
        public TaskCompletionSource Claimed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ClaimedJob?> ClaimAsync(CancellationToken ct) { Claimed.TrySetResult(); return Task.FromResult<ClaimedJob?>(null); }
        public Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> RetryAsync(Lease lease, int seconds, string? error, CancellationToken ct) => throw new NotSupportedException();
        public Task<WriteOutcome> ReleaseAsync(Lease lease, int seconds, string? error, CancellationToken ct) => throw new NotSupportedException();
    }
}
