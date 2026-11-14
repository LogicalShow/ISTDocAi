using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ResumePolicyService;
public interface IResumePolicyService
{
    ResumeAction Decide(int takeovers, string? state, string stage, int unknownCount);
}
