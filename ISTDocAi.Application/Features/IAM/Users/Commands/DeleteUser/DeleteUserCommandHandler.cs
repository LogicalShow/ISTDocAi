using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler(IUserManagementService userManagementService) : ICommandHandler<DeleteUserCommand, DeleteUserCommandResponse>
{
    public async Task<Result<DeleteUserCommandResponse>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Result<DeleteUserCommandResponse>.Failure(Error.Validation(ErrorCodes.UserRequired, "At least one user must be specified for deletion"));

        var result = await userManagementService.DeleteAsync(request.Items, cancellationToken);

        if (!result.IsSuccess)
            return Result<DeleteUserCommandResponse>.Failure(result.Error);

        return Result<DeleteUserCommandResponse>.Success(new DeleteUserCommandResponse("User(s) deleted successfully"));
    }
}
