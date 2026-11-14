using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate
{
    internal class AttachmentUpdateCommandValidator : AbstractValidator<AttachmentUpdateCommand>
    {
        public AttachmentUpdateCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.IdRequired);

            RuleFor(x => x.StoredFileName)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.StoredFileNameRequired);

            RuleFor(x => x.OriginalFileName)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.OriginalFileNameRequired);

            RuleFor(x => x.FilePath)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.FilePathRequired);

           

            RuleFor(x => x.FileSizeInBytes)
                .GreaterThan(0)
                .WithMessage(ValidationMessageKeys.FileSizeGreaterThanZero);

            RuleFor(x => x.ReferenceType)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.ReferenceTypeRequired);

            RuleFor(x => x.ReferenceId)
                .NotEmpty()
                .WithMessage(ValidationMessageKeys.ReferenceIdRequired);

            RuleFor(x => x.RowVersion)
                .NotNull()
                .WithMessage(ValidationMessageKeys.RowVersionRequired);
        }
    }
}
