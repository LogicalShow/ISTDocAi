using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Usage.Queries.GetUsage;
public sealed class GetUsageQueryHandler(IUsageReader reader, TimeProvider clock) : IQueryHandler<GetUsageQuery, GetUsageResponse>
{
    public async Task<Result<GetUsageResponse>> Handle(GetUsageQuery request, CancellationToken cancellationToken)
    {
        var b = await reader.GetUsageAsync(request.ConsumerId, request.Months, clock.GetUtcNow(), cancellationToken);
        return Result.Success(GetUsageResponse.From(b));
    }
}
