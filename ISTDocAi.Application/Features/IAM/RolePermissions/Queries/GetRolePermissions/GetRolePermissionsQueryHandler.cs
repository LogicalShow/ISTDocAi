using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.RolePermissions.Queries.GetRolePermissions;

public class GetRolePermissionsQueryHandler(IRolePermissionService rolePermissionService)
    : IQueryHandler<GetRolePermissionsQuery, RolePermissionsDto>
{
    public Task<Result<RolePermissionsDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken) =>
        rolePermissionService.GetRolePermissionsAsync(request.RoleId, cancellationToken);
}
