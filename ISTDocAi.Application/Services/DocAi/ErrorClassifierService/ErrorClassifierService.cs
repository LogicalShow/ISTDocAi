using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
public sealed class ErrorClassifierService : IErrorClassifierService
{
    public ClassifiedError Classify(ErrorSource source, int? statusCode = null, string? message = null)
    {
        var(kind, code) = source switch
        {
            ErrorSource.LeaseLost => (ErrorKind.LostLease, ErrorCodes.InternalError),
            ErrorSource.Configuration => (ErrorKind.Configuration, ErrorCodes.AzureAuth),
            ErrorSource.AnalysisFailed => (ErrorKind.Fatal, ErrorCodes.AnalysisFailed),
            ErrorSource.OriginalMissing => (ErrorKind.Fatal, ErrorCodes.OriginalMissing),
            ErrorSource.Unclassified => (ErrorKind.Fatal, ErrorCodes.Unclassified),
            ErrorSource.ModelMissing => (ErrorKind.Fatal, ErrorCodes.ModelNotFound),
            ErrorSource.Timeout or ErrorSource.Mapping or ErrorSource.Deadlock => (ErrorKind.Retryable, ErrorCodes.InternalError),
            _ => statusCode switch
            {
                401 or 403 => (ErrorKind.Configuration, ErrorCodes.AzureAuth),
                408 or 429 or >= 500 and <= 599 => (ErrorKind.Retryable, ErrorCodes.InternalError),
                400 => (ErrorKind.Fatal, ErrorCodes.InvalidDocument),
                404 => (ErrorKind.Fatal, source == ErrorSource.GetPoll ? ErrorCodes.AnalysisResultMissing : ErrorCodes.ModelNotFound),
                >= 400 and <= 499 when source == ErrorSource.PostAnalyze => (ErrorKind.Fatal, ErrorCodes.InvalidDocument),
                _ => (ErrorKind.Fatal, ErrorCodes.InternalError)}};
        return new(kind, new(code, message), kind == ErrorKind.Configuration);
    }
}
