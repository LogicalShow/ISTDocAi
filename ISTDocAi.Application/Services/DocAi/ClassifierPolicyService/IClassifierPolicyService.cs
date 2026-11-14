using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
public interface IClassifierPolicyService
{
    DocumentRoute? ResolveRoute(string requestedType, string? targetType, string? mappedLine, double confidence, double minimumConfidence);
}
