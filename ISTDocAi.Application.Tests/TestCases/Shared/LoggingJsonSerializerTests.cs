using ISTDocAi.Application.Common.Interfaces.Serialization;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Tests.TestCases.Shared;

[TestFixture]
public class LogPayloadSerializerTests
{
    [Test]
    public void Serialize_ShouldUseLoggingConfiguration()
    {
        using var scope = ServiceProviderFactory.CreateServiceProviderScope();
        var serializer = scope.ServiceProvider
            .GetRequiredService<ILogPayloadSerializer>();
        var value = new { Type = ErrorType.BusinessRule };

        var json = serializer.Serialize(value);

        Assert.That(json, Is.EqualTo("{\"Type\":\"BusinessRule\"}"));
    }

    [Test]
    public void Serialize_ShouldIgnoreReferenceCycles()
    {
        using var scope = ServiceProviderFactory.CreateServiceProviderScope();
        var serializer = scope.ServiceProvider
            .GetRequiredService<ILogPayloadSerializer>();
        var value = new CyclicValue();
        value.Next = value;

        var json = serializer.Serialize(value);

        Assert.That(json, Is.EqualTo("{\"Next\":null}"));
    }

    private sealed class CyclicValue
    {
        public CyclicValue? Next { get; set; }
    }
}
