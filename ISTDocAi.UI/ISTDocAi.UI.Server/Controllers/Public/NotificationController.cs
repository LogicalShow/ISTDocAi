using MediatR;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Authorization;

namespace ISTDocAi.UI.Server.Controllers.Public;

[Route("api/public/[controller]/[action]/{Id?}")]
public class NotificationController(IMediator mediator) : BaseController
{
    //Get: /api/public/Notification/NotificationById/{Id}
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_NOT, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(string Id, CancellationToken ct)
    {
        var result = await mediator.Send(new ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails.NotificationGetWithDetailsQuery(Id), ct);

        return Respond(result);

    }
    //Post : /api/public/Notification/NotificationList
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_NOT, ActionDataEnum.VIEW)]
    public async Task<IActionResult> GetList([FromBody] ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetList.NotificationGetListQuery query, CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Respond(result);
    }

    //Get:/api/public/Notification/SearchForSelect?term=xxx&pageSize=20
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_NOT, ActionDataEnum.VIEW)]
    public async Task<IActionResult> SearchForSelect([FromQuery] string? term,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ISTDocAi.Application.Features.Public.Notification.Queries.NotificationSearchForSelectWithExceptExistedNotificationUsers.NotificationSearchForSelectWithExceptExistedNotificationUsersQuery(term, pageSize);
        var result = await mediator.Send(query, ct);
        return Respond(result);
    }
}
