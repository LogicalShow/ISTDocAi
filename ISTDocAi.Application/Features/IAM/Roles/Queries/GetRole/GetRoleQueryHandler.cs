using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRole;

public class GetRoleQueryHandler(IRoleService roleService) : IQueryHandler<GetRoleQuery, GetRoleResponse>
{
    public async Task<Result<GetRoleResponse>> Handle(GetRoleQuery request, CancellationToken cancellationToken)
    {
        var role = await roleService.GetByIdAsync(request.Id, cancellationToken);
        if (role is null)
            return Result<GetRoleResponse>.Failure(Error.NotFound(ErrorCodes.RoleNotFound, "Role not found"));

        return Result<GetRoleResponse>.Success(new GetRoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            NameEn = role.NameEn,
            NameAr = role.NameAr,
            IsActive = role.IsActive,
            DisplayOrder = role.DisplayOrder,
            ConcurrencyStamp = role.ConcurrencyStamp
        });
    }
}
