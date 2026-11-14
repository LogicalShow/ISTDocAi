using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.LeasePolicyService;
public interface ILeasePolicyService
{
    LeaseWriteResult Check(JobStatus status, Guid? currentToken, Guid suppliedToken);
    LeaseWriteResult FromRowsChanged(int rowsChanged);
}
