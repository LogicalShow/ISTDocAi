using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Helpers.Models;
using ISTDocAi.UI.Server.Services.Public.Setting;
using ISTDocAi.UI.Server.Authorization;

namespace ISTDocAi.UI.Server.Controllers.Public;

[Route("api/public/[controller]/[action]/{Id?}")]
public class SettingController(ISettingWebService settingWebService) : BaseController
{
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_SET, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var setting = await settingWebService.GetSettingAsync(cancellationToken);
        return Respond(setting);
    }

    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_SET, ActionDataEnum.EDIT)]
    public async Task<IActionResult> Update([FromForm] UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        var result = await settingWebService.UpdateSetting(request, cancellationToken);
        return Respond(result);
    }

    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_SET, ActionDataEnum.VIEW)]
    // This method allows fetching an image based on the provided ImageType query parameter. LOGO=>1, ReportHeader=>2, ReportFooter=>3
    public async Task<IActionResult> GetImage([FromQuery] int imageType, CancellationToken cancellationToken)
    {
        var result = await settingWebService.GetImage((ImageType)imageType, cancellationToken);

        return Respond(result, image =>
            File(image.BinaryData, image.ContentType, image.FileName));
    }
}
