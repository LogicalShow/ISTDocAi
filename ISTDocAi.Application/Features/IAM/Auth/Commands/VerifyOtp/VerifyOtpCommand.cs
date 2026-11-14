using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Email,
    string Otp
) : ICommand<VerifyOtpCommandResponse>;