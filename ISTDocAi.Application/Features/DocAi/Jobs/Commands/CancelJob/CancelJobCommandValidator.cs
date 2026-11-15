using FluentValidation;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.CancelJob;
// Ordered validation stays in the handler to preserve the standard intake 400 response.
public sealed class CancelJobCommandValidator : AbstractValidator<CancelJobCommand> { }
