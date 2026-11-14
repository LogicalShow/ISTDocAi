using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests;
[NonParallelizable]
public sealed class TelemetryCoexistenceTests
{
    [Test]
    public void Standard_factory_keeps_additional_providers_and_suppresses_EF_without_duplicates()
    {
        using var capture = new Capture();
        using var factory = new BackendFactory(false).WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(capture)));
        using var client = factory.CreateClient();
        var logging = factory.Services.GetRequiredService<ILoggerFactory>();
        Assert.That(logging, Is.TypeOf<LoggerFactory>());
        Assert.That(factory.Services.GetServices<ILoggerProvider>().Count(p => p.GetType().FullName == "Serilog.Extensions.Logging.SerilogLoggerProvider"), Is.EqualTo(1));
        logging.CreateLogger("DocAi.SafeTest").LogInformation("{event}", "keyvault.unavailable");
        logging.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogError("TEST_SQL_SENTINEL");
        Assert.That(capture.Lines.Count(x => x == "keyvault.unavailable"), Is.EqualTo(1));
        Assert.That(capture.Lines, Does.Not.Contain("TEST_SQL_SENTINEL"));
    }
    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string category) => new Logger(this);
        public void Dispose() { }
        private sealed class Logger(Capture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) => owner.Lines.Enqueue(format(state, error));
        }
    }
}
