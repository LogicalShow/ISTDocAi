using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails.Contracts;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ChangeCurrentUserPassword;

internal class ChangeCurrentUserPasswordCommandHandler(
    IUserService userService,
    IUserQueryService userQueryService,
    IOtpService otpService,
    IUserPasswordService userPasswordService)
    : IRequestHandler<
        ChangeCurrentUserPasswordCommand,
        Result<ChangeCurrentUserPasswordCommandResponse>>
{
    public async Task<Result<ChangeCurrentUserPasswordCommandResponse>> Handle(ChangeCurrentUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = userService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result<ChangeCurrentUserPasswordCommandResponse>.Failure(Error.Unauthorized(ErrorCodes.AuthUserIdentityMissing, "User ID not found in token"));

        var email = await userQueryService.GetUserEmailByIdAsync(userId,cancellationToken);
        if (string.IsNullOrEmpty(email))
            return Result<ChangeCurrentUserPasswordCommandResponse>.Failure(Error.NotFound(ErrorCodes.UserEmailNotFound, "User email is not set"));

        if (!otpService.TryGetOtp(email, out var storedOtp) || storedOtp != request.Otp)
        {
            return Result<ChangeCurrentUserPasswordCommandResponse>.Failure(Error.BusinessRule(ErrorCodes.OtpInvalid, "Invalid or expired OTP"));
        }
        var result = await userPasswordService.ResetPasswordAsync(
                 userId,
                 request.NewPassword,
                 cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<ChangeCurrentUserPasswordCommandResponse>.Failure(Error.Validation(ErrorCodes.PasswordChangeFailed, result.Error.Message));
        }

        otpService.RemoveOtp(email);
        return Result<ChangeCurrentUserPasswordCommandResponse>.Success(new ChangeCurrentUserPasswordCommandResponse("Password has been changed successfully"));
    }
}
