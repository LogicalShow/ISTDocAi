using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.UserNameRequired);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.PasswordRequired);
    }
}
