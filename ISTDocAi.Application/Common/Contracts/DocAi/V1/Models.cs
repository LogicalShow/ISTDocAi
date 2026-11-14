using System.Text.Json.Serialization;
namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

public sealed record ModelIdChange(string? OldModelId, string? NewModelId);
public sealed record RunChanges(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelIdChange? Classifier = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelIdChange? ExtractModel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Mapping = null);

public sealed record SubmitJobResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RereadSkipped { get; init; }
    public required Guid JobId { get; init; }
    public required int RunNo { get; init; }
    public string RunReason { get => field ?? "New"; init; } = "New";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunChanges? Changes { get; init; }
    public required string Status { get; init; }
    public required string StatusUrl { get; init; }
    public required int PageCount { get; init; }
}
public record JobResponse : IJsonOnDeserialized
{
    public required Guid JobId { get; init; }
    public required int RunNo { get; init; }
    public required string ClientRequestId { get; init; }
    public string RunReason { get => field ?? "New"; init; } = "New";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunChanges? Changes { get; init; }
    public required string Status { get; init; }
    public required JobError? Error { get; init; }
    public required ICanonicalResult? Result { get; init; }
    public string? RequestedDocumentType { get; init; }
    public string? DocumentType { get; init; }
    public string? ResultType { get; init; }
    void IJsonOnDeserialized.OnDeserialized()
    {
        CanonicalResultCodec.ValidateType(ResultType);
        if (Result is not null && (ResultType is not null && ResultType != CanonicalResultCodec.TypeOf(Result)
            || DocumentType is not null && DocumentType != Result.DocumentTypeCode))
            throw new System.Text.Json.JsonException("Canonical result and envelope metadata disagree.");
    }
}
public sealed record WebhookEnvelope : JobResponse
{
    public required Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
public sealed record JobError(string Code, string Message);
public sealed record CatalogResponse(string ConsumerCode, CatalogItem[] Items, AutoDetectItem[] AutoDetect);
public sealed record CatalogItem(string DocumentTypeCode, string? ShippingLineCode, double MinFieldConfidence);
public sealed record AutoDetectItem(string DocumentTypeCode);
public sealed record CanonicalResult : ICanonicalResult
{
    public required string ContractVersion { get; init; }
    public required string DocumentTypeCode { get; init; }
    public required string? ShippingLineCode { get; init; }
    public required string ModelId { get; init; }
    public required int PageCount { get; init; }
    public required Dictionary<string, ExtractedField> Fields { get; init; }
    public required ContainerFields[] Containers { get; init; }
    public required FieldFlag[] Flags { get; init; }
}
public sealed record ExtractedField
{
    public required string? Value { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RawText { get; init; }
    public required double Confidence { get; init; }
    public required int Page { get; init; }
}
// Undetected fields are omitted; never manufacture a value or page for them.
public sealed record ContainerFields(
    ExtractedField ContainerNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExtractedField? SealNumber = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExtractedField? ContainerType = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExtractedField? Packages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExtractedField? GrossWeightKg = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExtractedField? MeasurementCbm = null);
public sealed record FieldFlag(string Field, string Reason);
