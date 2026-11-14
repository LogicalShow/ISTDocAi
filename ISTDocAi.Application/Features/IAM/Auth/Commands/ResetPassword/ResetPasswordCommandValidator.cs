using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.EmailRequired)
            .EmailAddress()
            .WithMessage(ValidationMessageKeys.EmailInvalid);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.NewPasswordRequired);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.OtpRequired);
    }
}
