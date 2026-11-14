using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Helpers.Queries.Public.ActionLog;

public class ActionLogGetQueryValidator : AbstractValidator<ActionLogGetQuery>
{
    public ActionLogGetQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);
    }
}
