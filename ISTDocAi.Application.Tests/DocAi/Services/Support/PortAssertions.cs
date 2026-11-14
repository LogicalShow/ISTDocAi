using NUnit.Framework;

namespace ISTDocAi.Application.Tests.DocAi.Services.Support;
internal static class PortAssertions
{
    public static T Single<T>(IEnumerable<T> values)
    {
        var items = values.ToArray();
        Assert.That(items, Has.Length.EqualTo(1));
        return items[0];
    }

    public static T IsType<T>(object? value)
    {
        Assert.That(value, Is.TypeOf<T>());
        return (T)value!;
    }

    public static void All<T>(IEnumerable<T> values, Action<T> assertion)
    {
        foreach (var value in values)
            assertion(value);
    }
}
