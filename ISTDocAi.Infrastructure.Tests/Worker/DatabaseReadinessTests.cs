using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class DatabaseReadinessTests
{
    
    [TestCase("jobs", false)] [TestCase("jobs", true)]
    [TestCase("webhooks", false)] [TestCase("webhooks", true)]
    [TestCase("retention", false)] [TestCase("retention", true)]
    [TestCase("signals", false)] [TestCase("signals", true)]
    public async Task Every_service_waits_before_even_creating_a_scope(string service, bool failed)
    {
        var gate = new DatabaseReadiness();
        var scopes = new CountingScopes();
        var clock = new TestClock();
        using BackgroundService worker = service switch {
            "jobs" => new JobProcessorService(scopes, Options.Create(new WorkerOptions()), new WorkerFakes(), clock, new TestLogger<JobProcessorService>(), gate),
            "webhooks" => new WebhookSenderService(scopes, Options.Create(new WebhookOptions()), clock, new TestLogger<WebhookSenderService>(), gate),
            "retention" => new RetentionService(scopes, Options.Create(new RetentionOptions()), clock, new TestLogger<RetentionService>(), gate),
            _ => new SignalsService(scopes, Options.Create(new SignalsOptions()), clock, new TestLogger<SignalsService>(), gate)
        };
        await worker.StartAsync(default);
        await Task.Delay(50);
        clock.Advance(TimeSpan.FromDays(2));
        if (failed) gate.MarkFailed("DATABASE_MISSING");
        await Task.Delay(50);
        Assert.Equal(0, scopes.Calls);
        Assert.Empty(clock.Delays);
        if (!failed)
        {
            gate.MarkReady();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (scopes.Calls == 0 && clock.Delays.Count == 0) await Task.Delay(5, timeout.Token);
            if (service is "retention" or "signals") clock.Advance(TimeSpan.FromDays(1));
            while (scopes.Calls == 0) await Task.Delay(5, timeout.Token);
        }
        await worker.StopAsync(default);
        if (failed) Assert.Equal(0, scopes.Calls);
    }
    private sealed class CountingScopes : IServiceScopeFactory
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public IServiceScope CreateScope() { Interlocked.Increment(ref calls); throw new InvalidOperationException("Test stops at dependency boundary."); }
    }
}
