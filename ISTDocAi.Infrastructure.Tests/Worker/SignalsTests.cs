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
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class SignalsTests
{
    [Test]
    public async Task Schedules_threshold_null_billing_failures_and_clean_stop_use_fake_time()
    {
        var clock = new TestClock();
        var reader = new Reader();
        var log = new TestLogger<SignalsService>();
        using var provider = new ServiceCollection().AddSingleton<ISignalReader>(reader).BuildServiceProvider();
        using var service = new SignalsService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new SignalsOptions()), clock, log, ReadyDatabase.Instance);
        await service.StartAsync(default);
        await Until(() => clock.Delays.Count == 2);
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, reader.QueueReads);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Until(() => reader.QueueReads == 1);
        Assert.Empty(log.Lines);
        reader.Age = 300;
        clock.Advance(TimeSpan.FromMinutes(1));
        await Until(() => reader.QueueReads == 2);
        Assert.Empty(log.Lines);
        reader.Age = 301;
        clock.Advance(TimeSpan.FromMinutes(1));
        await Until(() => log.Lines.Any(x => x.Text.Contains(AlertEvents.QueueStuck)));
        Assert.Contains(log.Lines, x => x.Level == LogLevel.Warning && x.Text.Contains("301"));
        reader.Throw = true;
        clock.Advance(TimeSpan.FromMinutes(57));
        await Until(() => reader.BillingReads == 1 && log.Lines.Count(x => x.Text.Contains("signals.read_failed")) == 2);
        Assert.DoesNotContain(log.Lines, x => x.Text.Contains("PRIVATE"));
        reader.Throw = false;
        clock.Advance(TimeSpan.FromHours(1));
        await Until(() => log.Lines.Count(x => x.Text.Contains(AlertEvents.UsageUnpriced)) == 2);
        Assert.All(log.Lines.Where(x => x.Text.Contains(AlertEvents.UsageUnpriced)), x => Assert.Equal(LogLevel.Warning, x.Level));
        await service.StopAsync(default);
        var reads = reader.QueueReads + reader.BillingReads;
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(reads, reader.QueueReads + reader.BillingReads);
    }
    [Test]
    public async Task Disabled_service_never_resolves_SQL()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var service = new SignalsService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SignalsOptions { Enabled = false }), new TestClock(), new TestLogger<SignalsService>(), ReadyDatabase.Instance);
        await service.StartAsync(default);
        await service.StopAsync(default);
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
        await Task.Delay(10, timeout.Token);
    }
    private sealed class Reader : ISignalReader
    {
        public int QueueReads;
        public int BillingReads;
        public int? Age;
        public bool Throw;
        public Task<int?> OldestDueSecondsAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref QueueReads);
            if (Throw) throw new IOException("PRIVATE SQL DETAILS");
            return Task.FromResult(Age);
        }
        public Task<IReadOnlyList<UnpricedUsage>> UnpricedUsageAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref BillingReads);
            if (Throw) throw new IOException("PRIVATE SQL DETAILS");
            return Task.FromResult<IReadOnlyList<UnpricedUsage>>([new("extract", DateTimeOffset.UnixEpoch, 2, 4), new("classify", DateTimeOffset.UnixEpoch, 1, 2)]);
        }
    }
}
