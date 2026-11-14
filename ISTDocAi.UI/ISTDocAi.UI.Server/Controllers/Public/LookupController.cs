using ISTDocAi.Application.Features.Public.Lookup.Queries.LookupGetList;
using ISTDocAi.UI.Server.Base;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ISTDocAi.UI.Server.Controllers.Public;

[Route("api/public/[controller]/[action]")]
public sealed class LookupController(IMediator mediator) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LookupGetListQuery(), cancellationToken);
        return Respond(result);
    }
}
