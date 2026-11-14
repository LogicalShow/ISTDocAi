using FluentValidation;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert;

public class NotificationUserUpsertCommandValidator : AbstractValidator<NotificationUserUpsertCommand>
{
    public NotificationUserUpsertCommandValidator()
    {
        RuleFor(x => x.NewNotificationId)
            .NotEmpty();

        RuleFor(x => x.SelectedNotificationUsers)
            .NotNull();

        // Pub_NotificationUser has a unique index on (NotificationId, UserId). Without this rule a
        // duplicated user in one submission surfaces as a database error instead of a validation one.
        RuleFor(x => x.SelectedNotificationUsers)
            .Must(users => users == null
                           || users.Select(u => u.UserId).Distinct().Count() == users.Count)
            .WithMessage("The same user cannot be assigned to a notification more than once.");
    }
}
