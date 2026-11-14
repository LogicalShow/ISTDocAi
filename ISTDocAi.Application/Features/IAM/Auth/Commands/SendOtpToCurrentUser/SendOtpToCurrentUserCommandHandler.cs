using ISTDocAi.Application.Common.EmailTemplates;
using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpToCurrentUser;

internal class SendOtpToCurrentUserCommandHandler(
    IUserService userService,
    IUserQueryService userQueryService,
    IOtpService otpService,
    IEmailService emailService)
    : IRequestHandler<SendOtpToCurrentUserCommand, Result<SendOtpToCurrentUserCommandResponse>>
{
     public async Task<Result<SendOtpToCurrentUserCommandResponse>> Handle(SendOtpToCurrentUserCommand request, CancellationToken cancellationToken)
    {
        var userId = userService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Result<SendOtpToCurrentUserCommandResponse>.Failure(Error.Unauthorized(ErrorCodes.AuthUserIdentityMissing, "User ID not found in token"));

        var email = await userQueryService.GetUserEmailByIdAsync(userId,cancellationToken);

        if (string.IsNullOrEmpty(email))
            return Result<SendOtpToCurrentUserCommandResponse>.Failure(Error.NotFound(ErrorCodes.UserEmailNotFound, "User email is not set"));

        var otp = new Random().Next(100000, 999999).ToString();
        otpService.SaveOtp(email, otp, OtpDefaults.Lifetime);

        var htmlBody = AuthEmailTemplates.BuildOtpEmail(otp);
        var emailResult = await emailService.SendEmailAsync(email, "Your OTP Code", htmlBody, cancellationToken);
        if (!emailResult.IsSuccess)
        {
            return Result<SendOtpToCurrentUserCommandResponse>.Failure(Error.Unexpected(ErrorCodes.EmailSendFailed, "Failed to send OTP email"));
        }

        return Result<SendOtpToCurrentUserCommandResponse>.Success(new SendOtpToCurrentUserCommandResponse("OTP sent to email"));
    }
}
