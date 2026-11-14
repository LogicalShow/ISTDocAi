using ISTDocAi.Application.Features.IAM.Roles.Commands.AddRole;
using ISTDocAi.Application.Features.IAM.Roles.Commands.DeleteRole;
using ISTDocAi.Application.Features.IAM.Roles.Commands.UpdateRole;
using ISTDocAi.Application.Features.IAM.Roles.Queries.GetRole;
using ISTDocAi.Application.Features.IAM.Roles.Queries.GetRoleList;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;
using ISTDocAi.UI.Server.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/IAM/[controller]/[action]/{Id?}")]
public class RoleController(IMediator mediator) : BaseController
{
    // GET: api/IAM/Role/Get/{id}
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(string Id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRoleQuery(Id), cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/Role/GetList
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.VIEW)]
    public async Task<IActionResult> GetList([FromBody] RoleGetListRequest request, CancellationToken cancellationToken)
    {
        var query = new GetRoleListQuery(request.Name, request.SortField, request.SortOrder, request.PageNo, request.PageRowCount);
        var result = await mediator.Send(query, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/Role/Add
    [HttpPost]
    [EnableIdempotency]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.ADD)]
    public async Task<IActionResult> Add([FromBody] RoleAddRequest request, CancellationToken cancellationToken)
    {
        var command = new AddRoleCommand(request.NameEn, request.NameAr, request.IsActive, request.DisplayOrder);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/Role/Update
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.EDIT)]
    public async Task<IActionResult> Update([FromBody] RoleUpdateRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(request.Id, request.NameEn, request.NameAr, request.IsActive, request.DisplayOrder, request.ConcurrencyStamp);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // POST: api/IAM/Role/Delete
    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.DELETE)]
    public async Task<IActionResult> Delete([FromBody] RoleDeleteRequest request, CancellationToken cancellationToken)
    {
        var command = new DeleteRoleCommand(request.ToDeleteItems());
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }
}
