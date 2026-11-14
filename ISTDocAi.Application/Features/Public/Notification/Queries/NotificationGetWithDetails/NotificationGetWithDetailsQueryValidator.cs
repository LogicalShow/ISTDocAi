using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails;

public class NotificationGetWithDetailsQueryValidator : AbstractValidator<NotificationGetWithDetailsQuery>
{
    public NotificationGetWithDetailsQueryValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);
    }
}
