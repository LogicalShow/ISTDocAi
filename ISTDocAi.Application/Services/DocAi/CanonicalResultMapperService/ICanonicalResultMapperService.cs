using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.CanonicalResultMapperService;
public interface ICanonicalResultMapperService
{
    Decision<CanonicalResult> Map(ExtractedDocument? document, double minFieldConfidence);
}
