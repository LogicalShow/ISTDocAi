using ISTDocAi.Application.Features.Public.NotificationUserDetail.Commands.MarkAsRead;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata;
using ISTDocAi.UI.Server.Base;

namespace ISTDocAi.UI.Server.Controllers.Public
{
    [Route("api/public/[controller]/[action]/{Id?}")]

    public class NotificationUserDetailController(IMediator mediator) : BaseController
    {
        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadCommand command, CancellationToken ct)
        {
          var result=  await mediator.Send(command, ct);
            return Respond(result);
        }
    }
}
