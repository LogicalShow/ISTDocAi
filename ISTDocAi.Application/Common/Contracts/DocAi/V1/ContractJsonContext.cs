using System.Text.Json;
using System.Text.Json.Serialization;
namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SubmitJobResponse))]
[JsonSerializable(typeof(JobResponse))]
[JsonSerializable(typeof(WebhookEnvelope))]
[JsonSerializable(typeof(CatalogResponse))]
[JsonSerializable(typeof(UsageResponse))]
[JsonSerializable(typeof(TopUpCyclesResponse))]
[JsonSerializable(typeof(CycleDocumentsResponse))]
[JsonSerializable(typeof(UsageMonth[]))]
[JsonSerializable(typeof(CanonicalResult))]
[JsonSerializable(typeof(ICanonicalResult))]
[JsonSerializable(typeof(CanonicalManifestResult))]
[JsonSerializable(typeof(JobError))]
public partial class ContractJsonContext : JsonSerializerContext;

public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(ContractJsonContext.Default.Options);
        options.MakeReadOnly();
        return options;
    }
}
