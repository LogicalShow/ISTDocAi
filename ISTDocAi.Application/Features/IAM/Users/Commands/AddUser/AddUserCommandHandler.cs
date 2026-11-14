using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.AddUser;

public class AddUserCommandHandler(IUserManagementService userManagementService) : ICommandHandler<AddUserCommand, AddUserCommandResponse>
{
    public async Task<Result<AddUserCommandResponse>> Handle(AddUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email))
            return Result<AddUserCommandResponse>.Failure(Error.Validation(ErrorCodes.UserRequired, "Username and Email are required"));

        var result = await userManagementService.AddAsync(
            request.UserName,
            request.Email,
            request.FullName,
            request.Password,
            request.IsActive,
            request.IsAdministrator,
            request.RoleId,
            cancellationToken);

        if (!result.IsSuccess)
            return Result<AddUserCommandResponse>.Failure(result.Error);

        return Result<AddUserCommandResponse>.Success(new AddUserCommandResponse(result.Value));
    }
}
