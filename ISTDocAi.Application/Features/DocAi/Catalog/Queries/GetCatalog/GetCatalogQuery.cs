using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces;
namespace ISTDocAi.Application.Features.DocAi.Catalog.Queries.GetCatalog;
public sealed record GetCatalogQuery(Consumer Consumer) : IQuery<GetCatalogResponse>;
