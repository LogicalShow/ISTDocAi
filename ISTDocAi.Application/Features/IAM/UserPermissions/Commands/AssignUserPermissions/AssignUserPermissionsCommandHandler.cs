using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.UserPermissions.Commands.AssignUserPermissions;

public class AssignUserPermissionsCommandHandler(IUserPermissionService userPermissionService)
    : ICommandHandler<AssignUserPermissionsCommand>
{
    public Task<Result> Handle(AssignUserPermissionsCommand request, CancellationToken cancellationToken) =>
        userPermissionService.AssignUserPermissionsAsync(
            request.UserId,
            request.PageActionIds,
            request.SourceRoleId,
            cancellationToken);
}
