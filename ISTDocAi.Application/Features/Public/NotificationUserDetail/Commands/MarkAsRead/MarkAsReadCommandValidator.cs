using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.NotificationUserDetail.Commands.MarkAsRead;

public class MarkAsReadCommandValidator : AbstractValidator<MarkAsReadCommand>
{
    public MarkAsReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);
    }
}
