using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Constants.DocAi;
public static class ErrorCodes
{
    public const string ModelNotFound = "MODEL_NOT_FOUND";
    public const string Unclassified = "UNCLASSIFIED";
    public const string InvalidDocument = "INVALID_DOCUMENT";
    public const string AnalysisFailed = "ANALYSIS_FAILED";
    public const string AnalysisResultMissing = "ANALYSIS_RESULT_MISSING";
    public const string OriginalMissing = "ORIGINAL_MISSING";
    public const string RetriesExhausted = "RETRIES_EXHAUSTED";
    public const string WorkerCrashLoop = "WORKER_CRASH_LOOP";
    // Internal Jobs.ErrorCode diagnostics while Queued; never public run outcomes, events or API errors.
    public const string AzureAuth = "AZURE_AUTH";
    public const string AzureThrottled = "AZURE_THROTTLED";
    public const string AzureUnavailable = "AZURE_UNAVAILABLE";
    public const string MappingFailed = "MAPPING_FAILED";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
