using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentAdd
{
    public class AttachmentAddCommandValidator : AbstractValidator<AttachmentAddCommand>
    {
        public AttachmentAddCommandValidator()
        {
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
                .WithMessage(ValidationMessageKeys.ReferenceIdRequired)
                .MaximumLength(100)
                .WithMessage(ValidationMessageKeys.ReferenceIdMaxLength);




        }
    }
}
