using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Serialization;

namespace ISTDocAi.Infrastructure.Services.DocAi.Webhooks;

public sealed class DocAiV2Json : IDocAiV2Json
{
    public JsonSerializerOptions Options { get; } = SharedJsonConfiguration.CreateOptions();
}
