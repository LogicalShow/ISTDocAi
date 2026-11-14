using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;

namespace ISTDocAi.Application.Services.DocAi.ResumePolicyService;
public sealed class ResumePolicyService(ITakeoverPolicyService _takeoverPolicyService) : IResumePolicyService
{
    public ResumeAction Decide(int takeovers, string? state, string stage, int unknownCount) => _takeoverPolicyService.IsExhausted(takeovers) ? ResumeAction.FailCrashLoop : state == "Accepted" ? ResumeAction.Poll : state == "Sending" ? ResumeAction.CloseUnknown : stage == "Map" ? ResumeAction.Map : unknownCount >= 2 ? ResumeAction.FailRetriesExhausted : ResumeAction.Submit;
}
