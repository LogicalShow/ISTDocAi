using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetList;

public class NotificationGetListQueryValidator : AbstractValidator<NotificationGetListQuery>
{
    private static readonly HashSet<string> SupportedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "GeneralMessageEn",
        "GeneralMessageAr"
    };

    public NotificationGetListQueryValidator()
    {
        RuleFor(x => x.PageNo)
            .GreaterThan(0);

        RuleFor(x => x.PageRowCount)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.SortField)
            .Must(sortField => string.IsNullOrWhiteSpace(sortField)
                || SupportedSortFields.Contains(sortField.Trim()))
            .WithMessage(ValidationMessageKeys.SortFieldNotSupported);

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => string.IsNullOrWhiteSpace(sortOrder)
                || sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage(ValidationMessageKeys.SortOrderInvalid);
    }
}
