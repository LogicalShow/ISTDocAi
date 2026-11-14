using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IRolePermissionService
{
    Task<Result<RolePermissionsDto>> GetRolePermissionsAsync(
        string roleId,
        CancellationToken cancellationToken);

    Task<Result> AssignRolePermissionsAsync(
        string roleId,
        IReadOnlyCollection<string> pageActionIds,
        CancellationToken cancellationToken);
}
