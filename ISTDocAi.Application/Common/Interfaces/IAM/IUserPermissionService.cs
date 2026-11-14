using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IUserPermissionService
{
    Task<Result<UserPermissionsDto>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<Result> AssignUserPermissionsAsync(
        string userId,
        IReadOnlyCollection<string> pageActionIds,
        string? sourceRoleId,
        CancellationToken cancellationToken);
}
