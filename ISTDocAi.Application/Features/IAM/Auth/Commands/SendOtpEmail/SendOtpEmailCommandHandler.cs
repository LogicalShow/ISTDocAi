using ISTDocAi.Application.Common.EmailTemplates;
using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;


namespace ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpEmail;

public class SendOtpEmailCommandHandler(IOtpService otpService, IEmailService emailService, IUserQueryService userQueryService) : IRequestHandler<SendOtpEmailCommand, Result<SendOtpEmailCommandResponse>>
{
    public async Task<Result<SendOtpEmailCommandResponse>> Handle(SendOtpEmailCommand request, CancellationToken cancellationToken)
    {
        var user =await userQueryService.GetUserByEmailAsync(request.Email,cancellationToken);

        if (user is null)
            return Result<SendOtpEmailCommandResponse>.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        var otp = new Random().Next(100000, 999999).ToString();
        otpService.SaveOtp(request.Email, otp, OtpDefaults.Lifetime);

        var htmlBody = AuthEmailTemplates.BuildOtpEmail(otp);
        var emailResult = await emailService.SendEmailAsync(request.Email, "Your OTP Code", htmlBody, cancellationToken);
        if (!emailResult.IsSuccess)
        {
            return Result<SendOtpEmailCommandResponse>.Failure(emailResult.Error);
        }
            return Result<SendOtpEmailCommandResponse>.Success(new SendOtpEmailCommandResponse("OTP sent to email"));
    }

}
