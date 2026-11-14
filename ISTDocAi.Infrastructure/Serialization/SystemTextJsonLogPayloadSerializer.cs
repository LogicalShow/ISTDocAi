using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.Serialization;

namespace ISTDocAi.Infrastructure.Serialization;

internal sealed class SystemTextJsonLogPayloadSerializer
    : ILogPayloadSerializer
{
    private static readonly JsonSerializerOptions Options =
        SharedJsonConfiguration.CreateOptions();

    public string Serialize<TValue>(TValue value)
    {
        return JsonSerializer.Serialize(value, Options);
    }
}
