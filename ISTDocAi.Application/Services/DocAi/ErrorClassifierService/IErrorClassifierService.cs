using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
public interface IErrorClassifierService
{
    ClassifiedError Classify(ErrorSource source, int? statusCode = null, string? message = null);
}
