using System.Globalization;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Common.Constants.Localization;

namespace ISTDocAi.Application.Common.Localization;

public sealed class CurrentUiCultureContext : ICultureContext
{
    public string Culture => ResolveCulture();

    public bool IsArabic => Culture == CultureConstants.Arabic;

    private static string ResolveCulture()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        return CultureConstants.Supported.Contains(culture)
            ? culture
            : CultureConstants.Default;
    }
}
