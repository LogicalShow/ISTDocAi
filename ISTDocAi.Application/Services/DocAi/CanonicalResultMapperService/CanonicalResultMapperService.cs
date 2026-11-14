using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.Iso6346Service;

namespace ISTDocAi.Application.Services.DocAi.CanonicalResultMapperService;
public sealed class CanonicalResultMapperService(IIso6346Service _iso6346Service) : ICanonicalResultMapperService
{
    public Decision<CanonicalResult> Map(ExtractedDocument? document, double minFieldConfidence)
    {
        if (document is null || document.Fields is null || !ValidConfidence(minFieldConfidence) || document.PageCount <= 0 || string.IsNullOrWhiteSpace(document.DocumentTypeCode) || string.IsNullOrWhiteSpace(document.ModelId) || document.Containers.IsDefault)
            return Invalid("Expected document metadata and confidence threshold in [0,1].");
        var fields = ImmutableDictionary.CreateBuilder<string, ExtractedField>(StringComparer.Ordinal);
        var flags = ImmutableArray.CreateBuilder<FieldFlag>();
        foreach (var(name, field)in document.Fields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(name))
                return Invalid("A field name cannot be empty.");
            if (field is null)
                continue;
            if (!ValidField(field, document.PageCount))
                return Invalid($"Invalid confidence or page for {name}.");
            fields.Add(name, field);
            FlagConfidence(name, field);
        }

        var containers = document.Containers.ToBuilder();
        for (var i = 0; i < document.Containers.Length; i++)
        {
            var container = document.Containers[i];
            if (container is null || container.ContainerNumber is null || !ValidField(container.ContainerNumber, document.PageCount) || (container.SealNumber is not null && !ValidField(container.SealNumber, document.PageCount)))
                return Invalid("Invalid container field confidence or page.");
            container = container with
            {
                ContainerNumber = container.ContainerNumber with
                {
                    Value = _iso6346Service.Normalize(container.ContainerNumber.Value),
                    RawText = container.ContainerNumber.RawText ?? (_iso6346Service.Normalize(container.ContainerNumber.Value) != container.ContainerNumber.Value ? container.ContainerNumber.Value : null)
                }
            };
            containers[i] = container;
            var name = $"containers[{i}].containerNumber";
            FlagConfidence(name, container.ContainerNumber);
            if (!_iso6346Service.IsValid(container.ContainerNumber.Value))
                flags.Add(new(name, FlagReasons.CheckDigit));
            if (container.SealNumber is not null)
                FlagConfidence($"containers[{i}].sealNumber", container.SealNumber);
            foreach (var(key, field)in new[]
            {
                ("containerType", container.ContainerType),
                ("packages", container.Packages),
                ("grossWeightKg", container.GrossWeightKg),
                ("measurementCbm", container.MeasurementCbm)
            }

            )
            {
                if (field is null)
                    continue;
                if (!ValidField(field, document.PageCount))
                    return Invalid("Invalid container field confidence or page.");
                FlagConfidence($"containers[{i}].{key}", field);
            }
        }

        return Decision<CanonicalResult>.Accept(new(document.DocumentTypeCode, document.ShippingLineCode, document.ModelId, document.PageCount, fields.ToImmutable(), containers.ToImmutable(), flags.ToImmutable()));
        void FlagConfidence(string name, ExtractedField field)
        {
            if (field.Confidence < minFieldConfidence)
                flags.Add(new(name, FlagReasons.LowConfidence));
        }
    }

    private bool ValidConfidence(double confidence) => double.IsFinite(confidence) && confidence is >= 0 and <= 1;
    private bool ValidField(ExtractedField field, int pages) => ValidConfidence(field.Confidence) && field.Page >= 1 && field.Page <= pages;
    private Decision<CanonicalResult> Invalid(string message) => Decision<CanonicalResult>.Reject(Rejection.InvalidInput, message);
}
