using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.EmailRequired)
            .EmailAddress()
            .WithMessage(ValidationMessageKeys.EmailInvalid);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.OtpRequired);
    }
}
