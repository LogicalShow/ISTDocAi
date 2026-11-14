using System.Globalization;
using System.Text.RegularExpressions;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestNumberParserService;
public sealed class ManifestNumberParserService : IManifestNumberParserService
{
    public bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 256)
            return false;
        // Only a single numeric token with optional surrounding unit letters is accepted.
        var match = Regex.Match(text, @"^\s*[A-Za-z]*\s*(\d+(?:[.,\s]\d+)*)\s*[A-Za-z]*\s*$");
        if (!match.Success)
            return false;
        var token = match.Groups[1].Value;
        if (Regex.IsMatch(token, @"\s") && !Regex.IsMatch(token, @"^\d{1,3}(?:\s\d{3})+(?:[.,]\d+)?$"))
            return false;
        token = Regex.Replace(token, @"\s", "");
        var dot = token.LastIndexOf('.');
        var comma = token.LastIndexOf(',');
        if (dot >= 0 && comma >= 0)
        {
            var separator = dot > comma ? '.' : ',';
            var thousands = separator == '.' ? ',' : '.';
            var parts = token.Split(separator);
            if (parts.Length != 2 || !ValidGroups(parts[0], thousands))
                return false;
            token = parts[0].Replace(thousands.ToString(), "") + "." + parts[1];
        }
        else if (dot >= 0 || comma >= 0)
        {
            var separator = dot >= 0 ? '.' : ',';
            var parts = token.Split(separator);
            if (parts.Length > 2 || parts[1].Length == 3)
            {
                if (!ValidGroups(token, separator))
                    return false;
                token = token.Replace(separator.ToString(), "");
            }
            else
                token = token.Replace(separator, '.');
        }

        return decimal.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private bool ValidGroups(string token, char separator)
    {
        var parts = token.Split(separator);
        return parts[0].Length is >= 1 and <= 3 && parts.Skip(1).All(p => p.Length == 3);
    }
}
