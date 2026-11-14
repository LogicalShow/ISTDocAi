using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class TestLogger<T> : ILogger<T>
{
    public ConcurrentQueue<(LogLevel Level, string Text)> Lines { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Enqueue((logLevel, formatter(state, exception)));
}
