using FluentValidation;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Helpers.Commands;

public class ExecuteServiceJobCommandValidator : AbstractValidator<ExecuteServiceJobCommand>
{
    public ExecuteServiceJobCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty();

        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage(ValidationMessageKeys.IdRequired);
    }
}
