using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.Login;

public class LoginCommandHandler(IUserAuthenticationService authenticationService)
    : ICommandHandler<LoginCommand, LoginCommandResponse>
{
    public async Task<Result<LoginCommandResponse>> Handle(
        LoginCommand request, 
        CancellationToken cancellationToken)
    {
        var authenticationResult = await authenticationService.AuthenticateAsync(
            request.Username,
            request.Password,
            request.ConnectionId,
            cancellationToken);

        if (!authenticationResult.IsSuccess)
        {
            return Result.Failure<LoginCommandResponse>(authenticationResult.Error);
        }

        return Result.Success(new LoginCommandResponse(
            authenticationResult.Value.AuthToken,
            authenticationResult.Value.FullName,
            authenticationResult.Value.Email));
    }
}
