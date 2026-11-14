using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.LeasePolicyService;
public sealed class LeasePolicyService : ILeasePolicyService
{
    // Expiry is deliberately not tested: only replacement of the token fences the old owner.
    public LeaseWriteResult Check(JobStatus status, Guid? currentToken, Guid suppliedToken) => status == JobStatus.Processing && suppliedToken != Guid.Empty && currentToken == suppliedToken ? LeaseWriteResult.Applied : LeaseWriteResult.LeaseLost;
    public LeaseWriteResult FromRowsChanged(int rowsChanged) => rowsChanged > 0 ? LeaseWriteResult.Applied : LeaseWriteResult.LeaseLost;
}
