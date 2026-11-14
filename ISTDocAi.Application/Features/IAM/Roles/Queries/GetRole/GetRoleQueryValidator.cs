using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRole;

public class GetRoleQueryValidator : AbstractValidator<GetRoleQuery>
{
    public GetRoleQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);
    }
}
