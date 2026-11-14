using System.Text.Json.Serialization;

namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

public sealed record CanonicalManifestResult : ICanonicalResult
{
    public required string ContractVersion { get; init; }
    public required string DocumentTypeCode { get; init; }
    public required string? ShippingLineCode { get; init; }
    public required string ModelId { get; init; }
    public required int PageCount { get; init; }
    public required Dictionary<string, ExtractedField> Header { get; init; }
    public required ManifestBill[] BillsOfLading { get; init; }
    public required UnidentifiedManifestBill[] UnidentifiedBillsOfLading { get; init; }
    public required ManifestContainer[] UnassignedContainers { get; init; }
    public required FieldFlag[] Flags { get; init; }
}

public sealed record UnidentifiedManifestBill(int SourceIndex, Dictionary<string, ExtractedField> Fields);

public sealed record ManifestBill(Dictionary<string, ExtractedField> Fields, ManifestContainer[] Containers);

// Partial rows may omit every field, including the container number.
public sealed record ManifestContainer
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? BillOfLadingNumber { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? ContainerNumber { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? SealNumber { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? ContainerType { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? Packages { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? GrossWeight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractedField? Description { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssociationMethod { get; init; }
}
