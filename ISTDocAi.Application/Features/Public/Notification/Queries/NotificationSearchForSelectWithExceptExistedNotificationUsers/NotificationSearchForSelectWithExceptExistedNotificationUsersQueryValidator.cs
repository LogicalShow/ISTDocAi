using FluentValidation;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationSearchForSelectWithExceptExistedNotificationUsers;

public class NotificationSearchForSelectWithExceptExistedNotificationUsersQueryValidator
    : AbstractValidator<NotificationSearchForSelectWithExceptExistedNotificationUsersQuery>
{
    public NotificationSearchForSelectWithExceptExistedNotificationUsersQueryValidator()
    {
        RuleFor(x => x.term)
            .MaximumLength(100);

        RuleFor(x => x.pageSize)
            .InclusiveBetween(1, 100);
    }
}
