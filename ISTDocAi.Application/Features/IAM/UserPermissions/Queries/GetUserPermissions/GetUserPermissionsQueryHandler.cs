using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.UserPermissions.Queries.GetUserPermissions;

public class GetUserPermissionsQueryHandler(IUserPermissionService userPermissionService)
    : IQueryHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    public Task<Result<UserPermissionsDto>> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken) =>
        userPermissionService.GetUserPermissionsAsync(request.UserId, cancellationToken);
}
