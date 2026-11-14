using System.Text.Json;
using System.Text.Json.Serialization;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Infrastructure.Serialization;

/// <summary>
/// Defines the JSON contract shared by the API and application-owned serializers.
/// </summary>
public static class SharedJsonConfiguration
{
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Add application-wide JSON settings here.
        options.PropertyNamingPolicy = null;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.WriteIndented = false;

        if (!options.Converters
                .OfType<JsonStringEnumConverter<ErrorType>>()
                .Any())
        {
            options.Converters.Add(new JsonStringEnumConverter<ErrorType>());
        }
    }

    internal static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Configure(options);

        return options;
    }
}
