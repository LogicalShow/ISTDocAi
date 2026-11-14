using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Dtos.Shared.DateRangeValidation;
using System.Linq.Expressions;

namespace ISTDocAi.Application.Common.Interfaces;

public interface IDateRangeOverlapChecker
{
    /// <summary>
    /// Validates a proposed date range against existing ranges for the same base predicate.
    /// Rules:
    /// - Overlap detection (half-open or closed).
    /// - Optional minimum duration.
    /// - Optional continuity rule: new range must start immediately after the last one.
    ///
    /// All comparisons use the values supplied by the caller.
    /// The skipInputConversion parameter is retained for compatibility and no longer changes behavior.
    /// basePredicate defines "which records participate" (parent, status, etc.).
    /// </summary>
    Task<DateRangeValidationResult> ValidateAsync<T>(
        IRepositoryOp<T> repository,
        Expression<Func<T, bool>> basePredicate,
        Expression<Func<T, DateTime?>> fromSelector,
        Expression<Func<T, DateTime?>> toSelector,
        DateTime from,
        DateTime? to,
        string dateRangeName,
        DateTimePrecision precision = DateTimePrecision.Full,
        bool halfOpen = false,
        bool requireNextFromAfterMaxTo = false,
        bool enforceMinDurationOneDay = false,
        bool skipInputConversion = false,
        CancellationToken cancellationToken = default)
        where T : Entity;
}


