using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.RolePermissions.Commands.AssignRolePermissions;

public class AssignRolePermissionsCommandValidator : AbstractValidator<AssignRolePermissionsCommand>
{
    public AssignRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        // Empty list is valid: it clears all role permissions.
        RuleFor(x => x.PageActionIds)
            .NotNull();

        RuleForEach(x => x.PageActionIds)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.ItemIdRequired);
    }
}
