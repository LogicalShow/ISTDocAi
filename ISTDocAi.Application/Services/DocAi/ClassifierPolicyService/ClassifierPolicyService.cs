using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
public sealed class ClassifierPolicyService : IClassifierPolicyService
{
    public DocumentRoute? ResolveRoute(string requestedType, string? targetType, string? mappedLine, double confidence, double minimumConfidence) => !string.IsNullOrWhiteSpace(targetType) && targetType != "AUTO" && (requestedType == "AUTO" ? targetType is "BL" or "MANIFEST" : targetType == requestedType) && double.IsFinite(confidence) && confidence is >= 0 and <= 1 && double.IsFinite(minimumConfidence) && minimumConfidence is >= 0 and <= 1 && confidence >= minimumConfidence ? new(targetType, mappedLine) : null;
}
