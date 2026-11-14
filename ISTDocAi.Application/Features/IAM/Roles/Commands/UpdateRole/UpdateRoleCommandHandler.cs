using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.UpdateRole;

public class UpdateRoleCommandHandler(IRoleService roleService) : ICommandHandler<UpdateRoleCommand, UpdateRoleCommandResponse>
{
    public async Task<Result<UpdateRoleCommandResponse>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.NameEn))
            return Result<UpdateRoleCommandResponse>.Failure(Error.Validation(ErrorCodes.RoleRequired, "Role ID and name (English) are required"));

        var result = await roleService.UpdateAsync(
            request.Id,
            request.NameEn,
            request.NameAr,
            request.IsActive,
            request.DisplayOrder,
            request.ConcurrencyStamp,
            cancellationToken);

        if (!result.IsSuccess)
            return Result<UpdateRoleCommandResponse>.Failure(result.Error);

        return Result<UpdateRoleCommandResponse>.Success(new UpdateRoleCommandResponse("Role updated successfully"));
    }
}
