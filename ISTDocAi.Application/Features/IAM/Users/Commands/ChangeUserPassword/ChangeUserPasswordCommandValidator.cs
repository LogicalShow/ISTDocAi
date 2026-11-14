using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.ChangeUserPassword;

public class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.NewPasswordRequired);
    }
}
