using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.UpdateRole;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        RuleFor(x => x.NameEn)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.NameEnRequired);
    }
}
