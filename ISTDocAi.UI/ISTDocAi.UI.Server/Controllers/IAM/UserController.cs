using ISTDocAi.Application.Features.IAM.Users.Commands.AddUser;
using ISTDocAi.Application.Features.IAM.Users.Commands.ChangeUserPassword;
using ISTDocAi.Application.Features.IAM.Users.Commands.DeleteUser;
using ISTDocAi.Application.Features.IAM.Users.Commands.UpdateUser;
using ISTDocAi.Application.Features.IAM.Users.Queries.GetUser;
using ISTDocAi.Application.Features.IAM.Users.Queries.GetUserList;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;
using ISTDocAi.UI.Server.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/IAM/[controller]/[action]/{Id?}")]
public class UserController(IMediator mediator) : BaseController
{
    // GET: api/IAM/User/Get/{id}
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(string Id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserQuery(Id), cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/User/GetList
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.VIEW)]
    public async Task<IActionResult> GetList([FromBody] UserGetListRequest request, CancellationToken cancellationToken)
    {
        var query = new GetUserListQuery(request.UserName, request.Email, request.SortField, request.SortOrder, request.PageNo, request.PageRowCount);
        var result = await mediator.Send(query, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/User/Add
    [HttpPost]
    [EnableIdempotency]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.ADD)]
    public async Task<IActionResult> Add([FromBody] UserAddRequest request, CancellationToken cancellationToken)
    {
        var command = new AddUserCommand(request.UserName, request.Email, request.FullName, request.Password, request.IsActive, request.IsAdministrator, request.RoleId);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/User/Update
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.EDIT)]
    public async Task<IActionResult> Update([FromBody] UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(request.Id, request.UserName, request.Email, request.FullName, request.IsActive, request.IsAdministrator, request.RoleId, request.ConcurrencyStamp);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/User/Delete
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.DELETE)]
    public async Task<IActionResult> Delete([FromBody] UserDeleteRequest request, CancellationToken cancellationToken)
    {
        var command = new DeleteUserCommand(request.ToDeleteItems());
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/User/ChangePassword
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.EDIT)]
    public async Task<IActionResult> ChangePassword([FromBody] UserChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeUserPasswordCommand(request.Id, request.NewPassword, request.ConcurrencyStamp);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }
}
