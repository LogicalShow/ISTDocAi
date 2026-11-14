using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.AddRole;

public class AddRoleCommandValidator : AbstractValidator<AddRoleCommand>
{
    public AddRoleCommandValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.NameEnRequired);
    }
}
