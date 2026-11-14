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

using System.Reflection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class RetentionTests
{
    private sealed class SafeLogger : ILogger<RetentionService>
    {
        public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Text)> Lines { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Enqueue((level, formatter(state, exception) + exception?.ToString()));
    }
    private sealed class Purge : IRetention
    {
        public int Calls;
        public Func<CancellationToken, Task> Run = _ => Task.CompletedTask;
        public Task RunAsync(CancellationToken ct) { Interlocked.Increment(ref Calls); return Run(ct); }
    }
    private sealed class Harness : IDisposable
    {
        public TestClock Clock { get; } = new();
        public Purge Purge { get; } = new();
        public SafeLogger Logs { get; } = new();
        public ServiceProvider Provider { get; }
        public RetentionService Service { get; }
        public int Scopes, Disposals;
        private sealed class ScopedPurge(Harness owner) : IRetention, IDisposable
        {
            public Task RunAsync(CancellationToken ct) => owner.Purge.RunAsync(ct);
            public void Dispose() => Interlocked.Increment(ref owner.Disposals);
        }
        public Harness(int hour = 1, bool enabled = true)
        {
            Clock.Advance(TimeSpan.FromHours(hour - 12));
            var services = new ServiceCollection();
            services.AddScoped<IRetention>(_ => { Interlocked.Increment(ref Scopes); return new ScopedPurge(this); });
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Service = new(Provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new RetentionOptions { Enabled = enabled }), Clock, Logs, ReadyDatabase.Instance);
        }
        public void Dispose() { Service.Dispose(); Provider.Dispose(); }
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
    
    [TestCase(1, 1)] [TestCase(3, 23)] [TestCase(2, 0)]
    public async Task First_deadline_is_next_UTC_time_without_startup_catchup(int hour, int wait)
    {
        using var h = new Harness(hour); await h.Service.StartAsync(default);
        if (wait == 0) await Until(() => h.Purge.Calls == 1);
        else
        {
            await Until(() => h.Clock.Delays.Count > 0);
            Assert.Equal(TimeSpan.FromHours(wait), h.Clock.Delays[0]);
            h.Clock.Advance(TimeSpan.FromHours(wait) - TimeSpan.FromSeconds(1));
            Assert.Equal(0, h.Purge.Calls);
            h.Clock.Advance(TimeSpan.FromSeconds(1)); await Until(() => h.Purge.Calls == 1);
        }
        await Until(() => h.Disposals == 1); await h.Service.StopAsync(default);
    }
    [Test]
    public async Task Daily_runs_use_new_scopes_and_an_hour_long_run_does_not_shift_deadline()
    {
        using var h = new Harness();
        h.Purge.Run = ct => Task.Delay(TimeSpan.FromHours(1), h.Clock, ct);
        await h.Service.StartAsync(default); await Until(() => h.Clock.Delays.Count == 1);
        h.Clock.Advance(TimeSpan.FromHours(1)); await Until(() => h.Clock.Delays.Count == 2);
        h.Clock.Advance(TimeSpan.FromHours(1)); await Until(() => h.Clock.Delays.Count == 3);
        Assert.Equal(TimeSpan.FromHours(23), h.Clock.Delays[2]);
        for (var day = 0; day < 3; day++)
        {
            h.Clock.Advance(TimeSpan.FromHours(23)); await Until(() => h.Purge.Calls == day + 2 && h.Clock.Delays.Count == 4 + day * 2);
            h.Clock.Advance(TimeSpan.FromHours(1)); await Until(() => h.Disposals == day + 2 && h.Clock.Delays.Count == 5 + day * 2);
        }
        Assert.Equal(4, h.Scopes); Assert.Equal(4, h.Disposals);
        Assert.All(h.Logs.Lines.Where(l => l.Text.Contains("retention.completed")), l => Assert.Contains("durationMs=3600000", l.Text));
        await h.Service.StopAsync(default);
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Failure_is_safe_and_next_day_still_runs_with_deadlock_at_warning(bool deadlock)
    {
        using var h = new Harness();
        if (deadlock) Assert.Equal(1205, Deadlock().Number);
        h.Purge.Run = _ => throw (deadlock ? Deadlock() : new IOException("PRIVATE_SQL_AND_TOKEN"));
        await h.Service.StartAsync(default); await Until(() => h.Clock.Delays.Count == 1);
        h.Clock.Advance(TimeSpan.FromHours(1)); await Until(() => h.Clock.Delays.Count == 2);
        var failed = Assert.Single(h.Logs.Lines, l => l.Text.Contains("retention.failed"));
        Assert.Equal(deadlock ? LogLevel.Warning : LogLevel.Error, failed.Level);
        h.Purge.Run = _ => Task.CompletedTask;
        h.Clock.Advance(TimeSpan.FromDays(1)); await Until(() => h.Clock.Delays.Count == 3);
        Assert.Equal(2, h.Purge.Calls);
        Assert.Contains(h.Logs.Lines, l => l.Text.Contains("retention.completed"));
        Assert.All(h.Logs.Lines, l => Assert.DoesNotContain("PRIVATE", l.Text));
        await h.Service.StopAsync(default);
    }
    private static SqlException Deadlock()
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(c => c.GetParameters().Length == 9);
        var error = constructor.Invoke([1205, (byte)0, (byte)13, "PRIVATE_SERVER", "PRIVATE_SQL_AND_TOKEN", "PRIVATE_PROC", 1, 0, null]);
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, [error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            null, [typeof(SqlErrorCollection), typeof(string)], null)!.Invoke(null, [errors, "1.0"])!;
    }
    [Test]
    public async Task Disabled_never_resolves_or_runs()
    {
        using var h = new Harness(enabled: false); await h.Service.StartAsync(default);
        h.Clock.Advance(TimeSpan.FromDays(4)); await h.Service.StopAsync(default);
        Assert.Equal(0, h.Purge.Calls); Assert.Equal(0, h.Scopes); Assert.Empty(h.Clock.Delays);
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Stop_cancels_wait_or_active_command_cleanly(bool active)
    {
        using var h = new Harness(); var cancelled = false;
        h.Purge.Run = async ct => { try { await Task.Delay(Timeout.Infinite, ct); } finally { cancelled = ct.IsCancellationRequested; } };
        await h.Service.StartAsync(default); await Until(() => h.Clock.Delays.Count == 1);
        if (active) { h.Clock.Advance(TimeSpan.FromHours(1)); await Until(() => h.Purge.Calls == 1); }
        await h.Service.StopAsync(default); await h.Service.ExecuteTask!;
        Assert.Equal(active, cancelled); Assert.Equal(active ? 1 : 0, h.Disposals);
        h.Clock.Advance(TimeSpan.FromDays(2)); Assert.Equal(active ? 1 : 0, h.Purge.Calls);
        Assert.DoesNotContain(h.Logs.Lines, l => l.Level >= LogLevel.Warning);
    }
    
    [TestCase("00:00", true)] [TestCase("23:59:59", true)] [TestCase("-01:00", false)] [TestCase("24:00:00", false)]
    public void Registration_validates_time_of_day(string time, bool valid)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDocAiWorker(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:RunAtUtc"] = time }).Build());
        using var provider = services.BuildServiceProvider();
        if (valid) Assert.Equal(TimeSpan.Parse(time), provider.GetRequiredService<IOptions<RetentionOptions>>().Value.RunAtUtc);
        else Assert.ThrowsAny<Exception>(() => { _ = provider.GetRequiredService<IOptions<RetentionOptions>>().Value; });
        Assert.True(new RetentionOptions().Enabled); Assert.Equal(TimeSpan.FromHours(2), new RetentionOptions().RunAtUtc);
    }
}




