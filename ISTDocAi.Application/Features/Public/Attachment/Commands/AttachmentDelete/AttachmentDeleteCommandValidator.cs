using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentDelete
{
    internal class AttachmentDeleteCommandValidator : AbstractValidator<AttachmentDeleteCommand>
    {
        public AttachmentDeleteCommandValidator()
        {
            RuleFor(x => x.Items)
                .NotNull()
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.DeleteItemsRequired);

            RuleForEach(x => x.Items)
                .ChildRules(item =>
                {
                    item.RuleFor(x => x.Id)
                        .NotEmpty()
                        .WithMessage(ValidationMessageKeys.ItemIdRequired);

                    item.RuleFor(x => x.RowVersion)
                        .NotNull()
                        .NotEmpty()
                        .WithMessage(ValidationMessageKeys.ItemRowVersionRequired);
                });
        }
    }
}
