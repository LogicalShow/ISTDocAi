using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUserList;

public class GetUserListQueryValidator : AbstractValidator<GetUserListQuery>
{
    public GetUserListQueryValidator()
    {
        RuleFor(x => x.PageNo)
            .GreaterThan(0);

        RuleFor(x => x.PageRowCount)
            .GreaterThan(0);

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => string.IsNullOrWhiteSpace(sortOrder)
                || sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage(ValidationMessageKeys.SortOrderInvalid);
    }
}
