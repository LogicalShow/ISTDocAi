using System.Text.Json;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Infrastructure.Serialization;
using ISTDocAi.UI.Server.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;
using Microsoft.AspNetCore.SignalR;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class SharedJsonConfigurationTests
{
    [Test]
    public void Configure_ShouldSerializeErrorTypeAsString()
    {
        var options = new JsonSerializerOptions();
        SharedJsonConfiguration.Configure(options);

        var json = JsonSerializer.Serialize(ErrorType.BusinessRule, options);

        Assert.That(json, Is.EqualTo("\"BusinessRule\""));
    }

    [Test]
    public void Configure_ShouldKeepUnconfiguredEnumsNumeric()
    {
        var options = new JsonSerializerOptions();
        SharedJsonConfiguration.Configure(options);

        var json = JsonSerializer.Serialize(UnconfiguredType.Active, options);

        Assert.That(json, Is.EqualTo("1"));
    }

    [Test]
    public void Configure_ShouldBeIdempotent()
    {
        var options = new JsonSerializerOptions();

        SharedJsonConfiguration.Configure(options);
        SharedJsonConfiguration.Configure(options);

        Assert.That(options.Converters, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddApiJsonSerialization_ShouldConfigureEveryApiTransport()
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.AddSignalR().AddJsonProtocol();
        services.AddApiJsonSerialization();

        using var serviceProvider = services.BuildServiceProvider();
        var mvcOptions = serviceProvider
            .GetRequiredService<IOptions<MvcJsonOptions>>()
            .Value
            .JsonSerializerOptions;
        var httpOptions = serviceProvider
            .GetRequiredService<IOptions<HttpJsonOptions>>()
            .Value
            .SerializerOptions;
        var signalROptions = serviceProvider
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value
            .PayloadSerializerOptions;

        Assert.Multiple(() =>
        {
            Assert.That(SerializeErrorType(mvcOptions), Is.EqualTo("\"BusinessRule\""));
            Assert.That(SerializeErrorType(httpOptions), Is.EqualTo("\"BusinessRule\""));
            Assert.That(SerializeErrorType(signalROptions), Is.EqualTo("\"BusinessRule\""));
        });
    }

    private static string SerializeErrorType(JsonSerializerOptions options) =>
        JsonSerializer.Serialize(ErrorType.BusinessRule, options);

    private enum UnconfiguredType
    {
        None = 0,
        Active = 1
    }
}
