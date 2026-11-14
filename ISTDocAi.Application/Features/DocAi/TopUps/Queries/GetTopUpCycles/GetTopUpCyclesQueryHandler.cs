using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetTopUpCycles;
public sealed class GetTopUpCyclesQueryHandler(IUsageReader reader) : IQueryHandler<GetTopUpCyclesQuery, GetTopUpCyclesResponse>
{
    public async Task<Result<GetTopUpCyclesResponse>> Handle(GetTopUpCyclesQuery request, CancellationToken cancellationToken)
    {
        var b = await reader.GetTopUpCyclesAsync(request.ConsumerId, request.GrantId, cancellationToken)
            ?? throw new IntakeException(404, "TOPUP_NOT_FOUND", "Not found", "No such top-up cycle or cursor exists for this consumer.");
        return Result.Success(GetTopUpCyclesResponse.From(b));
    }
}
