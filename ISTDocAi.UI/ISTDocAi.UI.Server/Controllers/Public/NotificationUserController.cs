using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert;
using ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserList;
using ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserListPaginated;
using ISTDocAi.Application.Features.Public.NotificationUser.Queries.UserGetList;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;

namespace ISTDocAi.UI.Server.Controllers.Public
{
    [Route("api/public/[controller]/[action]/{Id?}")]
    public class NotificationUserController(IMediator mediator, INotificationService notificationService) : BaseController
    {
        [HttpPost]
        [RequirePermission(PageDataEnum.ADM_NOT, ActionDataEnum.EDIT)]
        public async Task<IActionResult> UpdateNotificationUsersAsync([FromBody] NotificationUserUpsertCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return Respond(result);
        }

        [HttpGet]
        [RequirePermission(PageDataEnum.ADM_NOT, ActionDataEnum.VIEW)]
        public async Task<IActionResult> GetUserListAsync(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new UserGetListQuery(), cancellationToken);
            return Respond(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotifications(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new NotificationUserListQuery(), cancellationToken);
            return Respond(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotificationsPaginated(
            int pageNo = 1, int pageRowCount = 10, bool unreadOnly = false,
            CancellationToken cancellationToken = default)
        {
            var result = await mediator.Send(
                new NotificationUserListPaginatedQuery(pageNo, pageRowCount, unreadOnly),
                cancellationToken);
            return Respond(result);
        }

        [HttpGet]
        public async Task<IActionResult> AddUserToAssienedNotificationGroups(CancellationToken cancellationToken)
        {
       
            await notificationService.AddUserToAssienedNotificationGroups(cancellationToken);
            return Respond();
        }
    }
}
