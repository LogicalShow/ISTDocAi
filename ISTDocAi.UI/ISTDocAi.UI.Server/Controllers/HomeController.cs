using Microsoft.AspNetCore.Mvc;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Base;

namespace ISTDocAi.UI.Server.Controllers;

[Route("api/[controller]/[action]")]
public class HomeController : BaseController
{
    [HttpGet]
    public IActionResult Get()
    {
        return Respond(Result.Success("home/get"));
    }
}
