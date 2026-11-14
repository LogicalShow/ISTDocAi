using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
public sealed class TakeoverPolicyService : ITakeoverPolicyService
{
    public bool IsExhausted(int takeoverCount) => takeoverCount >= 3;
}
