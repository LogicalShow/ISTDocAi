using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetCycleDocuments;
public sealed class GetCycleDocumentsQueryHandler(IUsageReader reader) : IQueryHandler<GetCycleDocumentsQuery, GetCycleDocumentsResponse>
{
    public async Task<Result<GetCycleDocumentsResponse>> Handle(GetCycleDocumentsQuery request, CancellationToken cancellationToken)
    {
        var b = await reader.GetCycleDocumentsAsync(request.ConsumerId, request.GrantId, request.Limit, request.AfterChargeId, cancellationToken)
            ?? throw new IntakeException(404, "TOPUP_NOT_FOUND", "Not found", "No such top-up cycle or cursor exists for this consumer.");
        return Result.Success(GetCycleDocumentsResponse.From(b));
    }
}
