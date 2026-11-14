using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.AddUser;

public class AddUserCommandValidator : AbstractValidator<AddUserCommand>
{
    public AddUserCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.UserNameRequired);

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.EmailRequired)
            .EmailAddress()
            .WithMessage(ValidationMessageKeys.EmailInvalid);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.PasswordRequired);
    }
}
