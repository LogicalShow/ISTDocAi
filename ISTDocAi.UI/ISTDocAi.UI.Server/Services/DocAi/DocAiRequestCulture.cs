using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
namespace ISTDocAi.UI.Server.Services.DocAi;
public static class DocAiRequestCulture
{
    public static async Task<CultureInfo> SelectAsync(HttpContext context)
    {
        // Reuse the template's first provider (X-Culture), without its query-string fallback
        // or setting CurrentCulture, which must stay invariant during intake.
        var provider = context.RequestServices.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value.RequestCultureProviders[0];
        var result = await provider.DetermineProviderCultureResult(context);
        var name = result?.UICultures.FirstOrDefault().ToString();
        return CultureInfo.GetCultureInfo(name is "ar" ? "ar" : "en");
    }
}
