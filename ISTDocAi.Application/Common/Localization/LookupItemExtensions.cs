using ISTDocAi.Application.Common.BaseModels;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Features.Public.Lookup.Queries.LookupGetList;

namespace ISTDocAi.Application.Common.Localization;

public static class LookupItemExtensions
{
    public static LocalizedLookupItem ToLocalized(this LookupItem item, ICultureContext culture) =>
        new(
            item.Id,
            item.Code,
            culture.IsArabic && !string.IsNullOrWhiteSpace(item.NameAr)
                ? item.NameAr!
                : item.NameEn);
}
