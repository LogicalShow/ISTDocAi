using ISTDocAi.Application.Features.IAM.UserMenu.Queries.GetUserMenu;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/IAM/[controller]")]
public class UserMenuController(IMediator mediator) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserMenuQuery(), cancellationToken);
        return Respond(result);
    }
}
