using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.UserPermissions.Commands.AssignUserPermissions;

public class AssignUserPermissionsCommandValidator : AbstractValidator<AssignUserPermissionsCommand>
{
    public AssignUserPermissionsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        // Empty list is valid: it clears all user permissions.
        RuleFor(x => x.PageActionIds)
            .NotNull();

        RuleForEach(x => x.PageActionIds)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.ItemIdRequired);
    }
}
