using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType
{
    internal class AttachmentGetListByReferenceIdAndReferenceTypeQueryValidator : AbstractValidator<AttachmentGetListByReferenceIdAndReferenceTypeQuery>
    {
        public AttachmentGetListByReferenceIdAndReferenceTypeQueryValidator()
        {
            RuleFor(x => x.ReferenceId)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.ReferenceIdRequired);

            RuleFor(x => x.ReferenceType)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.ReferenceTypeRequired);
        }
    }
}
