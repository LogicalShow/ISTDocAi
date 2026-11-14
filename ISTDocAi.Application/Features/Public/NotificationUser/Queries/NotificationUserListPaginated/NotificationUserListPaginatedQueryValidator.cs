using FluentValidation;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserListPaginated;

public class NotificationUserListPaginatedQueryValidator : AbstractValidator<NotificationUserListPaginatedQuery>
{
    public NotificationUserListPaginatedQueryValidator()
    {
        RuleFor(x => x.PageNo)
            .GreaterThan(0);

        RuleFor(x => x.PageRowCount)
            .InclusiveBetween(1, 100);
    }
}
