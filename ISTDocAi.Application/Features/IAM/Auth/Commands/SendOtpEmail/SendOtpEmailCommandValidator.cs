using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpEmail;

public class SendOtpEmailCommandValidator : AbstractValidator<SendOtpEmailCommand>
{
    public SendOtpEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.EmailRequired)
            .EmailAddress()
            .WithMessage(ValidationMessageKeys.EmailInvalid);
    }
}
