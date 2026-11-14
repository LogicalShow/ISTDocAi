using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.ChangeCurrentUserPassword;

public class ChangeCurrentUserPasswordCommandValidator : AbstractValidator<ChangeCurrentUserPasswordCommand>
{
    public ChangeCurrentUserPasswordCommandValidator()
    {
        RuleFor(x => x.Otp)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.OtpRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.NewPasswordRequired);
    }
}
