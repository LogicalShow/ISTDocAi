using ISTDocAi.Application.Features.IAM.RolePermissions.Queries.GetRolePermissions;
using ISTDocAi.Application.Features.IAM.UserPermissions.Commands.AssignUserPermissions;
using ISTDocAi.Application.Features.IAM.UserPermissions.Queries.GetUserPermissions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Authorization;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/IAM/[controller]/[action]/{Id?}")]
public class UserPermissionsController(IMediator mediator) : BaseController
{
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.VIEW)]
    public async Task<IActionResult> Get(string userId, CancellationToken cancellationToken)
    {
        var query = new GetUserPermissionsQuery(userId);
        var result = await mediator.Send(query, cancellationToken);
        return Respond(result);
    }

    [HttpPost]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.EDIT)]
    public async Task<IActionResult> Assign([FromBody] AssignUserPermissionsRequest request, CancellationToken cancellationToken)
    {
        var command = new AssignUserPermissionsCommand(request.UserId, request.PageActionIds, request.SourceRoleId);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    /// <summary>
    /// Read-only: returns the role's permission tree so the user permissions screen can
    /// preload it into the editor. Nothing is written - the caller applies the selection
    /// through <see cref="Assign"/>.
    /// </summary>
    [HttpGet]
    [RequirePermission(PageDataEnum.ADM_IAM_USR, ActionDataEnum.EDIT)]
    public async Task<IActionResult> GetRolePermissions(string roleId, CancellationToken cancellationToken)
    {
        var query = new GetRolePermissionsQuery(roleId);
        var result = await mediator.Send(query, cancellationToken);
        return Respond(result);
    }
}

public record AssignUserPermissionsRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<string> PageActionIds { get; set; } = new();
    public string? SourceRoleId { get; set; }
}
