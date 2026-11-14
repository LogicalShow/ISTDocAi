using System.Text.Json;
using System.Text.Json.Serialization;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
// Raw contract JSON also preserves escaping: template writer encoders cannot alter its bytes.
public sealed record GetJobResponse(Guid JobId, int RunNo, string ClientRequestId, string RunReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RunChanges? Changes,
    string Status, JobError? Error, [property: JsonConverter(typeof(CanonicalDocumentConverter))] ICanonicalResult? Result, string? RequestedDocumentType,
    string? DocumentType, string? ResultType);

public sealed class CanonicalDocumentConverter : JsonConverter<ICanonicalResult>
{
    public override ICanonicalResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        using var document = JsonDocument.ParseValue(ref reader);
        return CanonicalResultCodec.Deserialize(document.RootElement.GetRawText(), null);
    }
    public override void Write(Utf8JsonWriter writer, ICanonicalResult value, JsonSerializerOptions options) =>
        writer.WriteRawValue(CanonicalResultCodec.Serialize(value));
}
