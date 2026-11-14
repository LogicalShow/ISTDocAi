namespace ISTDocAi.Application.Common.Contracts.DocAi.V1;

public static class FieldNames
{
    public const string BillOfLadingNumber = "billOfLadingNumber";
    public const string ShipperName = "shipperName";
    public const string ConsigneeName = "consigneeName";
    public const string VesselName = "vesselName";
    public const string VoyageNumber = "voyageNumber";
    public const string PortOfLoading = "portOfLoading";
    public const string PortOfDischarge = "portOfDischarge";
    public const string IssueDate = "issueDate";
    public const string ContainerNumber = "containerNumber";
    public const string SealNumber = "sealNumber";
    public const string BookingNumber = "bookingNumber";
    public const string ShipperAddress = "shipperAddress";
    public const string ConsigneeAddress = "consigneeAddress";
    public const string NotifyParty = "notifyParty";
    public const string FreightTerms = "freightTerms";
    public const string HsCode = "hsCode";
    public const string TotalGrossWeightKg = "totalGrossWeightKg";
    public const string ContainerType = "containerType";
    public const string Packages = "packages";
    public const string GrossWeightKg = "grossWeightKg";
    public const string MeasurementCbm = "measurementCbm";

}

public static class FlagReasons
{
    public const string LowConfidence = "LOW_CONFIDENCE";
    public const string CheckDigit = "CHECK_DIGIT";
    public const string RowMerged = "ROW_MERGED";
    public const string BlRowDropped = "BL_ROW_DROPPED";
    public const string OrphanContainer = "ORPHAN_CONTAINER";
    public const string BlReferenceNotFound = "BL_REFERENCE_NOT_FOUND";
    public const string AmbiguousBlReference = "AMBIGUOUS_BL_REFERENCE";
    public const string AssociationPositionMissing = "ASSOCIATION_POSITION_MISSING";
    public const string TotalsMismatch = "TOTALS_MISMATCH";
    public const string TotalsUnverified = "TOTALS_UNVERIFIED";
    public const string AssociationByPosition = "ASSOCIATION_BY_POSITION";

}

public static class RunStatuses
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class EventTypes
{
    public const string JobCompleted = "job.completed";
    public const string JobFailed = "job.failed";
}

public static class ErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string UnsupportedFileType = "UNSUPPORTED_FILE_TYPE";
    public const string TooManyPages = "TOO_MANY_PAGES";
    public const string EncryptedPdf = "ENCRYPTED_PDF";
    public const string NotInCatalog = "NOT_IN_CATALOG";
    public const string DailyCapReached = "DAILY_CAP_REACHED";
    public const string RateLimited = "RATE_LIMITED";
    public const string JobNotFound = "JOB_NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ModelNotFound = "MODEL_NOT_FOUND";
    public const string Unclassified = "UNCLASSIFIED";
    public const string InvalidDocument = "INVALID_DOCUMENT";
    public const string AnalysisFailed = "ANALYSIS_FAILED";
    public const string AnalysisResultMissing = "ANALYSIS_RESULT_MISSING";
    public const string OriginalMissing = "ORIGINAL_MISSING";
    public const string RetriesExhausted = "RETRIES_EXHAUSTED";
    public const string WorkerCrashLoop = "WORKER_CRASH_LOOP";
    public const string AzureAuth = "AZURE_AUTH";
    public const string SubmitExhausted = "SUBMIT_EXHAUSTED";
    public const string LocalCopyMissing = "LOCAL_COPY_MISSING";
    public const string LocalCopyUnreadable = "LOCAL_COPY_UNREADABLE";
    public const string FileEmpty = "FILE_EMPTY";
    public const string UnsupportedFile = "UNSUPPORTED_FILE";
    public const string DemoFailure = "DEMO_FAILURE";
}

public static class ProblemDetailsExtensions
{
    public const string Code = "code";
    public const string CorrelationId = "correlationId";
}
