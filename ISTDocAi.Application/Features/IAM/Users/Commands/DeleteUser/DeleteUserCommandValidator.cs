using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.DeleteUser;

public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.DeleteItemsRequired);

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Id)
                    .NotEmpty()
                    .WithMessage(ValidationMessageKeys.ItemIdRequired);
            });
    }
}
