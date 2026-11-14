using System.Text.Json;

namespace ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;

public interface IDocAiV2Json
{
    JsonSerializerOptions Options { get; }
}
