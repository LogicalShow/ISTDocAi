using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.ChangeUserPassword;

public class ChangeUserPasswordCommandHandler(IUserManagementService userManagementService) : ICommandHandler<ChangeUserPasswordCommand, ChangeUserPasswordCommandResponse>
{
    public async Task<Result<ChangeUserPasswordCommandResponse>> Handle(ChangeUserPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.NewPassword))
            return Result<ChangeUserPasswordCommandResponse>.Failure(Error.Validation(ErrorCodes.UserRequired, "User ID and New Password are required"));

        var result = await userManagementService.ChangePasswordAsync(request.Id, request.NewPassword, request.ConcurrencyStamp, cancellationToken);

        if (!result.IsSuccess)
            return Result<ChangeUserPasswordCommandResponse>.Failure(result.Error);

        return Result<ChangeUserPasswordCommandResponse>.Success(new ChangeUserPasswordCommandResponse("Password changed successfully"));
    }
}
