using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ChangeCurrentUserPassword;
public record ChangeCurrentUserPasswordCommand(
    string Otp,
    string NewPassword
) : ICommand<ChangeCurrentUserPasswordCommandResponse>;