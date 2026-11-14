using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
public interface ITakeoverPolicyService
{
    bool IsExhausted(int takeoverCount);
}
