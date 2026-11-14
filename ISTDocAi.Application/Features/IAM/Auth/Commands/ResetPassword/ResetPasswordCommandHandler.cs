using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ResetPassword;

internal class ResetPasswordCommandHandler(IOtpService otpService, IUserPasswordService userPasswordService , IUserQueryService userQueryService) : IRequestHandler<ResetPasswordCommand, Result<ResetPasswordCommandResponse>>
{
    public async Task<Result<ResetPasswordCommandResponse>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!otpService.TryGetOtp(request.Email, out var storedOtp) || storedOtp != request.Otp)
        {
            return Result<ResetPasswordCommandResponse>.Failure(
                Error.BusinessRule(
                    ErrorCodes.OtpInvalid,
                    "Invalid or expired OTP"));
        }

        var user = await userQueryService.GetUserByEmailAsync(request.Email,cancellationToken);
        if (user == null)
            return Result<ResetPasswordCommandResponse>.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        var result = await userPasswordService.ResetPasswordAsync(user.Id, request.NewPassword, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<ResetPasswordCommandResponse>.Failure(Error.Validation(ErrorCodes.PasswordResetFailed, result.Error.Message));
        }

        otpService.RemoveOtp(request.Email);
        return Result<ResetPasswordCommandResponse>.Success(new ResetPasswordCommandResponse("Password has been reset"));
    }
}
