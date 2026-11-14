using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler(IOtpService otpService)
    : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpCommandResponse>>
{
    public Task<Result<VerifyOtpCommandResponse>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (otpService.TryGetOtp(request.Email, out var storedOtp) && storedOtp == request.Otp)
        {
            return Task.FromResult(
                Result<VerifyOtpCommandResponse>.Success(new VerifyOtpCommandResponse("OTP is valid")));
        }

        return Task.FromResult(
            Result<VerifyOtpCommandResponse>.Failure(
                Error.BusinessRule(
                    ErrorCodes.OtpInvalid,
                    "Invalid or expired OTP")));
    }
}
