using FluentValidation;
namespace ISTDocAi.Application.Features.DocAi.Catalog.Queries.GetCatalog;
// Intentionally no pipeline rules: parsing and ordered intake own all checks.
// An earlier rule could replace a 415/422 or change multi-invalid-input precedence.
public sealed class GetCatalogQueryValidator : AbstractValidator<GetCatalogQuery> { }
