using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.AddRole;

public class AddRoleCommandHandler(IRoleService roleService) : ICommandHandler<AddRoleCommand, AddRoleCommandResponse>
{
    public async Task<Result<AddRoleCommandResponse>> Handle(AddRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NameEn))
            return Result<AddRoleCommandResponse>.Failure(Error.Validation(ErrorCodes.RoleRequired, "Role name (English) is required"));

        var result = await roleService.AddAsync(
            request.NameEn,
            request.NameAr,
            request.IsActive,
            request.DisplayOrder,
            cancellationToken);

        if (!result.IsSuccess)
            return Result<AddRoleCommandResponse>.Failure(result.Error);

        return Result<AddRoleCommandResponse>.Success(new AddRoleCommandResponse(result.Value));
    }
}
