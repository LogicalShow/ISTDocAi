using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById
{
    internal class AttachmentGetByIdQueryValidator : AbstractValidator<AttachmentGetByIdQuery>
    {
        public AttachmentGetByIdQueryValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.IdRequired);
        }
    }
}
