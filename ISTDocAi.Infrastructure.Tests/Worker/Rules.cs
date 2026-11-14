using ISTDocAi.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

internal static class Rules
{
    private static readonly ServiceProvider Provider = new ServiceCollection().AddApplication().BuildServiceProvider();
    internal static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();
}
