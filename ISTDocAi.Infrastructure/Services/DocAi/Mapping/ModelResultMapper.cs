using ISTDocAi.Application.Common.Mappings.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;


using Microsoft.Extensions.Options;
using V1Contract = ISTDocAi.Application.Common.Contracts.DocAi.V1;

namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

// Dispatch by durable document type and select profiles using the producing operation model ID.
public sealed class ModelResultMapper(IMappingProfileResolver resolver)
{
    public MappedResult Map(string json, string documentType, string? line, string modelId, double minFieldConfidence)
    {
        if (documentType == "AUTO") throw new InvalidOperationException("Extraction route is unresolved.");
        return MapEffective(json, documentType, line, modelId, minFieldConfidence, resolver.Resolve(modelId, documentType, allowLegacyDocumentType: true));
    }

    internal static MappedResult MapEffective(string json, string documentType, string? line, string modelId,
        double minFieldConfidence, AnalyzeResultMappingOptions profile)
    {
        if (documentType == "MANIFEST")
        {
            var manifest = MappingServices.Manifest.Map(ManifestAnalyzeResultParser.Parse(json, line, modelId, (AnalyzeResultMappingProfile)profile), minFieldConfidence).Value
                ?? throw new InvalidOperationException("Invalid canonical manifest result.");
            return new(ToContract(manifest), manifest.FlaggedFields, V1Contract.ResultTypes.Manifest);
        }
        if (documentType == "AUTO") throw new InvalidOperationException("Extraction route is unresolved.");
        var bill = MappingServices.Bill.Map(AnalyzeResultParser.Parse(json, documentType, line, modelId,
            profile), minFieldConfidence).Value
            ?? throw new InvalidOperationException("Invalid canonical result.");
        return new(CanonicalResultProjection.BillOfLading(bill), bill.FlaggedFields, V1Contract.ResultTypes.BillOfLading);
    }

    public Decision<CanonicalManifestResult> MapManifest(string json, string? line, string modelId, double minFieldConfidence)
    {
        var profile = (AnalyzeResultMappingProfile)resolver.Resolve(modelId, "MANIFEST");
        return MappingServices.Manifest.Map(ManifestAnalyzeResultParser.Parse(json, line, modelId, profile), minFieldConfidence);
    }

    public Decision<CanonicalResult> MapBillOfLading(string json, string? line, string modelId, double minFieldConfidence) =>
        MappingServices.Bill.Map(AnalyzeResultParser.Parse(json, "BL", line, modelId,
            resolver.Resolve(modelId, "BL")), minFieldConfidence);

    public static V1Contract.CanonicalManifestResult ToContract(CanonicalManifestResult result) => new()
    {
        ContractVersion = result.ContractVersion, DocumentTypeCode = result.DocumentTypeCode,
        ShippingLineCode = result.ShippingLineCode, ModelId = result.ModelId, PageCount = result.PageCount,
        Header = Fields(result.Header),
        BillsOfLading = result.BillsOfLading.Select(b => new V1Contract.ManifestBill(Fields(b.Fields), b.Containers.Select(Container).ToArray())).ToArray(),
        UnidentifiedBillsOfLading = result.UnidentifiedBillsOfLading.Select(b => new V1Contract.UnidentifiedManifestBill(b.SourceIndex, Fields(b.Fields))).ToArray(),
        UnassignedContainers = result.UnassignedContainers.Select(Container).ToArray(),
        Flags = result.Flags.Select(f => new V1Contract.FieldFlag(f.Field, f.Reason)).ToArray()
    };

    private static Dictionary<string, V1Contract.ExtractedField> Fields(IEnumerable<KeyValuePair<string, ExtractedField>> fields) =>
        fields.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => new V1Contract.ExtractedField
        { Value = p.Value.Value, RawText = p.Value.RawText, Confidence = p.Value.Confidence, Page = p.Value.Page }, StringComparer.Ordinal);

    private static V1Contract.ManifestContainer Container(ManifestContainer container)
    {
        var fields = Fields(container.Fields);
        return new()
        {
            BillOfLadingNumber = fields.GetValueOrDefault("billOfLadingNumber"),
            ContainerNumber = fields.GetValueOrDefault("containerNumber"),
            SealNumber = fields.GetValueOrDefault("sealNumber"),
            ContainerType = fields.GetValueOrDefault("containerType"),
            Packages = fields.GetValueOrDefault("packages"),
            GrossWeight = fields.GetValueOrDefault("grossWeight"),
            Description = fields.GetValueOrDefault("description"),
            AssociationMethod = container.AssociationMethod
        };
    }
}
