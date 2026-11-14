using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.UserNameRequired);

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage(ValidationMessageKeys.EmailInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
