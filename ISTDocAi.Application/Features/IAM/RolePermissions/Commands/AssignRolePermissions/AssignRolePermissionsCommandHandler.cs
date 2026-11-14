using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.RolePermissions.Commands.AssignRolePermissions;

public class AssignRolePermissionsCommandHandler(IRolePermissionService rolePermissionService)
    : ICommandHandler<AssignRolePermissionsCommand>
{
    public Task<Result> Handle(AssignRolePermissionsCommand request, CancellationToken cancellationToken) =>
        rolePermissionService.AssignRolePermissionsAsync(request.RoleId, request.PageActionIds, cancellationToken);
}
