using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler(IUserManagementService userManagementService) : ICommandHandler<UpdateUserCommand, UpdateUserCommandResponse>
{
    public async Task<Result<UpdateUserCommandResponse>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.UserName))
            return Result<UpdateUserCommandResponse>.Failure(Error.Validation(ErrorCodes.UserRequired, "User ID and Username are required"));

        var result = await userManagementService.UpdateAsync(
            request.Id,
            request.UserName,
            request.Email,
            request.FullName,
            request.IsActive,
            request.IsAdministrator,
            request.RoleId,
            request.ConcurrencyStamp,
            cancellationToken);

        if (!result.IsSuccess)
            return Result<UpdateUserCommandResponse>.Failure(result.Error);

        return Result<UpdateUserCommandResponse>.Success(new UpdateUserCommandResponse("User updated successfully"));
    }
}
