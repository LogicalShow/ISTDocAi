using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.DeleteRole;

public class DeleteRoleCommandHandler(IRoleService roleService) : ICommandHandler<DeleteRoleCommand, DeleteRoleCommandResponse>
{
    public async Task<Result<DeleteRoleCommandResponse>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Result<DeleteRoleCommandResponse>.Failure(Error.Validation(ErrorCodes.RoleRequired, "At least one role must be specified for deletion"));

        var result = await roleService.DeleteAsync(request.Items, cancellationToken);

        if (!result.IsSuccess)
            return Result<DeleteRoleCommandResponse>.Failure(result.Error);

        return Result<DeleteRoleCommandResponse>.Success(new DeleteRoleCommandResponse("Role(s) deleted successfully"));
    }
}
