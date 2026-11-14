using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Constants.DocAi;
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
