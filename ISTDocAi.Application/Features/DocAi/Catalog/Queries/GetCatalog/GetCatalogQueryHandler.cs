using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Catalog.Queries.GetCatalog;
public sealed class GetCatalogQueryHandler(ISTDocAi.Application.Services.DocAi.Intake.GetCatalog service) : IQueryHandler<GetCatalogQuery, GetCatalogResponse>
{
    public async Task<Result<GetCatalogResponse>> Handle(GetCatalogQuery request, CancellationToken cancellationToken)
    {
        var b = await service.ExecuteAsync(request.Consumer, cancellationToken);
        return Result.Success(new GetCatalogResponse(b.ConsumerCode, b.Items, b.AutoDetect));
    }
}
