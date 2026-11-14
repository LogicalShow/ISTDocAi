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


using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class DocumentIntelligenceReadinessTests
{
    
    [TestCase(true)] [TestCase(false)]
    public async Task Success_and_failure_are_shared_across_concurrent_callers_for_exactly_sixty_seconds(bool ready)
    {
        var clock = new TestClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analyzer = new WorkerFakes { AccessHandler = async ct =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return ready;
        } };
        using var services = new ServiceCollection().AddSingleton<IDocumentAnalyzer>(analyzer).BuildServiceProvider();
        using var cache = new DocumentIntelligenceReadiness(services.GetRequiredService<IServiceScopeFactory>(), clock);
        var first = cache.CheckAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = Enumerable.Range(0, 19).Select(_ => cache.CheckAsync(default)).ToArray();
        PortAssert.All(waiting, check => PortAssert.False(check.IsCompleted));
        PortAssert.Equal(1, analyzer.AccessChecks);
        release.SetResult();
        PortAssert.All(await Task.WhenAll(waiting.Append(first)), value => PortAssert.Equal(ready, value));
        PortAssert.Equal(1, analyzer.AccessChecks);
        analyzer.AccessHandler = _ => Task.FromResult(!ready);
        clock.Advance(TimeSpan.FromSeconds(59));
        PortAssert.Equal(ready, await cache.CheckAsync(default));
        PortAssert.Equal(1, analyzer.AccessChecks);
        clock.Advance(TimeSpan.FromSeconds(1));
        PortAssert.Equal(!ready, await cache.CheckAsync(default));
        PortAssert.Equal(2, analyzer.AccessChecks);
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Exceptions_are_cached_as_not_ready_without_escaping(bool unavailable)
    {
        var clock = new TestClock();
        var analyzer = new WorkerFakes { AccessHandler = _ => throw (unavailable
            ? new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable)
            : new TaskCanceledException("PRIVATE diagnostic URL and token")) };
        using var services = new ServiceCollection().AddSingleton<IDocumentAnalyzer>(analyzer).BuildServiceProvider();
        using var cache = new DocumentIntelligenceReadiness(services.GetRequiredService<IServiceScopeFactory>(), clock);
        PortAssert.False(await cache.CheckAsync(default));
        PortAssert.False(await cache.CheckAsync(default));
        PortAssert.Equal(1, analyzer.AccessChecks);
        clock.Advance(TimeSpan.FromSeconds(60));
        PortAssert.False(await cache.CheckAsync(default));
        PortAssert.Equal(2, analyzer.AccessChecks);
    }
}
