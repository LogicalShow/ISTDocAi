using V1Contract = ISTDocAi.Application.Common.Contracts.DocAi.V1;
using DomainDto = ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Common.Mappings.DocAi;

public static class CanonicalResultProjection
{
    public static V1Contract.CanonicalResult BillOfLading(DomainDto.CanonicalResult result)
    {
        static V1Contract.ExtractedField Field(DomainDto.ExtractedField field) => new() { Value = field.Value, RawText = field.RawText, Confidence = field.Confidence, Page = field.Page };
        return new() { ContractVersion = result.ContractVersion, DocumentTypeCode = result.DocumentTypeCode,
            ShippingLineCode = result.ShippingLineCode, ModelId = result.ModelId, PageCount = result.PageCount,
            Fields = result.Fields.ToDictionary(f => f.Key, f => Field(f.Value)),
            Containers = result.Containers.Select(c => new V1Contract.ContainerFields(Field(c.ContainerNumber), c.SealNumber is null ? null : Field(c.SealNumber),
                c.ContainerType is null ? null : Field(c.ContainerType), c.Packages is null ? null : Field(c.Packages),
                c.GrossWeightKg is null ? null : Field(c.GrossWeightKg), c.MeasurementCbm is null ? null : Field(c.MeasurementCbm))).ToArray(),
            Flags = result.Flags.Select(f => new V1Contract.FieldFlag(f.Field, f.Reason)).ToArray() };
    }
}
