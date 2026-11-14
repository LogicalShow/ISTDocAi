using ISTDocAi.Application.Features.IAM.RolePermissions.Commands.AssignRolePermissions;
using ISTDocAi.Application.Features.IAM.RolePermissions.Queries.GetRolePermissions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Authorization;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/IAM/[controller]/[action]/{Id?}")]
public class RolePermissionsController(IMediator mediator) : BaseController
{
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(string roleId, CancellationToken cancellationToken)
    {
        var query = new GetRolePermissionsQuery(roleId);
        var result = await mediator.Send(query, cancellationToken);
        return Respond(result);
    }

    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_ROL, ActionDataEnum.EDIT)]
    public async Task<IActionResult> Assign([FromBody] AssignRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var command = new AssignRolePermissionsCommand(request.RoleId, request.PageActionIds);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }
}

public record AssignRolePermissionsRequest
{
    public string RoleId { get; set; } = string.Empty;
    public List<string> PageActionIds { get; set; } = new();
}
