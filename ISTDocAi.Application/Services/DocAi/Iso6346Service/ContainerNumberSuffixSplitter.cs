using System.Text.RegularExpressions;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.Iso6346Service;

public static class ContainerNumberSuffixSplitter
{
    private static readonly Iso6346Service Iso = new();
    private static readonly Regex GluedNumber = new(@"^\s*([A-Z]{3}[UJZ])\s*(\d{6})\s*(\d)\s*/\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex IsoType = new(@"^[124LM][0-9][GRUPTBVHSK][0-9A-Z]$", RegexOptions.CultureInvariant);
    // Size, then an apostrophe/FT and/or type letters, and nothing numeric after the size (so seals like "20A12345" stay unknown).
    private static readonly Regex SizeType = new(@"^(10|20|40|45|53)\s*(?:(?:['’]|FT)\s*(?:[A-Z][A-Z '’.-]*)?|[A-Z][A-Z '’.-]*)$", RegexOptions.CultureInvariant);

    public static (ExtractedField Number, ExtractedField? Type) Split(ExtractedField number, ExtractedField? seal, ExtractedField? type)
    {
        var match = GluedNumber.Match(number.Value ?? "");
        var clean = string.Concat(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value).ToUpperInvariant();
        if (!match.Success || !Iso.IsValid(clean))
        {
            var normalized = Iso.Normalize(number.Value);
            return (number with { Value = normalized, RawText = number.RawText ?? (normalized != number.Value ? number.Value : null) }, type);
        }

        var parts = new List<string>();
        var unknown = false;
        foreach (var suffix in match.Groups[4].Value.Split('/'))
        {
            var part = suffix.Trim().ToUpperInvariant();
            if (IsoType.IsMatch(part) || SizeType.IsMatch(part))
            {
                if (!TypeKey(type?.Value).Contains(TypeKey(part), StringComparison.OrdinalIgnoreCase)) parts.Add(part);
            }
            else if (SealKey(part).Length == 0 || !string.Equals(SealKey(part), SealKey(seal?.Value), StringComparison.OrdinalIgnoreCase))
                unknown = true;
        }

        if (parts.Count > 0)
        {
            var value = string.Join(" ", parts.Append(type?.Value ?? "")).Trim();
            type = type is null ? new(value, null, number.Confidence, number.Page) : type with { Value = value };
        }
        return (number with { Value = clean, RawText = number.RawText ?? number.Value, Confidence = unknown ? Math.Min(number.Confidence, .5) : number.Confidence }, type);
    }

    private static string TypeKey(string? value) => new((value ?? "").Where(c => !char.IsWhiteSpace(c) && c is not ('\'' or '’')).ToArray());
    private static string SealKey(string? value) => new((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
}
