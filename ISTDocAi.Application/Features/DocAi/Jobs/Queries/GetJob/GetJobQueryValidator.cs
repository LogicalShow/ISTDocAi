using FluentValidation;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
// No pipeline rules: transport validates raw strings in source order before dispatch.
// Revalidating typed inputs here could change precedence or reader call order.
public sealed class GetJobQueryValidator : AbstractValidator<GetJobQuery> { }
