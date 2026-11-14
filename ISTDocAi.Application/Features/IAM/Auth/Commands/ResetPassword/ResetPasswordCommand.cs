using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string NewPassword,
    string Otp) : ICommand<ResetPasswordCommandResponse>;