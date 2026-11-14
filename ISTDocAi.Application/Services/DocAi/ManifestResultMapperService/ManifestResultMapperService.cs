using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.Iso6346Service;
using ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestTotalsPolicyService;

namespace ISTDocAi.Application.Services.DocAi.ManifestResultMapperService;
public sealed class ManifestResultMapperService(IIso6346Service _iso6346Service, IManifestAssociationPolicyService _manifestAssociationPolicyService, IManifestRowMergePolicyService _manifestRowMergePolicyService, IManifestTotalsPolicyService _manifestTotalsPolicyService) : IManifestResultMapperService
{
    public Decision<CanonicalManifestResult> Map(ExtractedManifest? document, double minFieldConfidence)
    {
        if (document is null || document.PageCount <= 0 || string.IsNullOrWhiteSpace(document.ModelId) || !double.IsFinite(minFieldConfidence) || minFieldConfidence is < 0 or > 1 || document.Header is null || document.BillsOfLading.IsDefault || document.Containers.IsDefault)
            return Invalid();
        var allRows = document.BillsOfLading.Concat(document.Containers).ToArray();
        if (allRows.Any(r => r is null || r.Fields is null || r.Geometry is null || r.SourceIndex < 0) || document.BillsOfLading.Select(r => r.SourceIndex).Distinct().Count() != document.BillsOfLading.Length || document.Containers.Select(r => r.SourceIndex).Distinct().Count() != document.Containers.Length || document.Header.Concat(allRows.SelectMany(r => r.Fields)).Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value is null || !double.IsFinite(p.Value.Confidence) || p.Value.Confidence is < 0 or > 1 || p.Value.Page < 1 || p.Value.Page > document.PageCount))
            return Invalid();
        // Normalize before merging, association, validation and totals; retain all source metadata.
        document = document with
        {
            BillsOfLading = document.BillsOfLading.Select(NormalizeRow).ToImmutableArray(),
            Containers = document.Containers.Select(NormalizeRow).ToImmutableArray()
        };
        var flags = ImmutableArray.CreateBuilder<FieldFlag>();
        var merged = _manifestRowMergePolicyService.Merge(document.BillsOfLading, document.PageCount);
        var unidentified = _manifestAssociationPolicyService.Sort(merged.Rows.Where(r => _manifestAssociationPolicyService.Identifier(r).Length == 0), document.PageCount);
        for (var i = 0; i < unidentified.Length; i++)
        {
            var path = $"unidentifiedBillsOfLading[{i}]";
            flags.Add(new(path, FlagReasons.BlRowDropped));
            FlagFields(path + ".fields", unidentified[i].Fields);
        }

        var bills = _manifestAssociationPolicyService.Sort(merged.Rows.Where(r => _manifestAssociationPolicyService.Identifier(r).Length > 0), document.PageCount);
        for (var i = 0; i < bills.Length; i++)
            if (merged.ChangedFields.TryGetValue(bills[i].SourceIndex, out var changed))
            {
                flags.Add(new($"billsOfLading[{i}]", FlagReasons.RowMerged));
                foreach (var name in changed)
                    flags.Add(new($"billsOfLading[{i}].fields.{name}", FlagReasons.RowMerged));
            }

        var groups = bills.Select(_ => ImmutableArray.CreateBuilder<ManifestContainer>()).ToArray();
        var unassigned = ImmutableArray.CreateBuilder<ManifestContainer>();
        FlagFields("header", document.Header);
        for (var i = 0; i < bills.Length; i++)
            FlagFields($"billsOfLading[{i}].fields", bills[i].Fields);
        foreach (var row in _manifestAssociationPolicyService.Sort(document.Containers, document.PageCount))
        {
            var association = _manifestAssociationPolicyService.Associate(row, bills, document.PageCount);
            var target = association.BillIndex is { } bill ? groups[bill] : unassigned;
            var path = association.BillIndex is { } b ? $"billsOfLading[{b}].containers[{target.Count}]" : $"unassignedContainers[{target.Count}]";
            target.Add(new(row.Fields, association.Method));
            foreach (var reason in association.Reasons)
                flags.Add(new(path, reason));
            FlagFields(path, row.Fields);
            if (row.Fields.TryGetValue("containerNumber", out var number) && !_iso6346Service.IsValid(number.Value))
                flags.Add(new(path + ".containerNumber", FlagReasons.CheckDigit));
        }

        for (var i = 0; i < bills.Length; i++)
            flags.AddRange(_manifestTotalsPolicyService.Check(bills[i].Fields, groups[i].ToImmutable(), $"billsOfLading[{i}]"));
        return Decision<CanonicalManifestResult>.Accept(new(document.ShippingLineCode, document.ModelId, document.PageCount, document.Header, bills.Select((r, i) => new ManifestBill(r.Fields, groups[i].ToImmutable())).ToImmutableArray(), unidentified.Select(r => new UnidentifiedManifestBill(r.SourceIndex, r.Fields)).ToImmutableArray(), unassigned.ToImmutable(), flags.OrderBy(f => f.Field, StringComparer.Ordinal).ThenBy(f => f.Reason, StringComparer.Ordinal).ToImmutableArray()));
        void FlagFields(string path, ImmutableDictionary<string, ExtractedField> fields)
        {
            foreach (var(name, field)in fields.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (field.Confidence < minFieldConfidence)
                    flags.Add(new(path + "." + name, FlagReasons.LowConfidence));
        }
    }

    private ManifestRow NormalizeRow(ManifestRow row) => row.Fields.TryGetValue("containerNumber", out var field) ? row with
    {
        Fields = row.Fields.SetItem("containerNumber", field with { Value = _iso6346Service.Normalize(field.Value), RawText = field.RawText ?? (_iso6346Service.Normalize(field.Value) != field.Value ? field.Value : null) })
    }

    : row;
    private Decision<CanonicalManifestResult> Invalid() => Decision<CanonicalManifestResult>.Reject(Rejection.InvalidInput, "Expected manifest metadata, valid fields and confidence threshold in [0,1].");
}
