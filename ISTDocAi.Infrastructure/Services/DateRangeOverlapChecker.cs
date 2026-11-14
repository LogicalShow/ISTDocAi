using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.Shared.DateRangeValidation;
using System.Linq.Expressions;

namespace ISTDocAi.Infrastructure.Services;

/// <summary>
/// Validates date range overlaps and continuity.
/// NOTE: For Minutes/Hours/Seconds precision, only input values are truncated (not database columns).
/// Ensure database doesn't store milliseconds if using Seconds precision for accurate comparisons.
/// </summary>
public class DateRangeOverlapChecker : IDateRangeOverlapChecker
{
    public async Task<DateRangeValidationResult> ValidateAsync<T>(
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
        where T : Entity
    {
        if (precision is DateTimePrecision.Minutes or DateTimePrecision.Hours or DateTimePrecision.Seconds && halfOpen)
        {
            throw new NotSupportedException("Half-open intervals are not supported with Minutes/Hours/Seconds precision");
        }

        var result = new DateRangeValidationResult();

        var fromUtc = from;
        DateTime? toUtc = to;

        // 2) Normalize to precision
        fromUtc = ApplyPrecision(fromUtc, precision);
        if (toUtc.HasValue)
            toUtc = ApplyPrecision(toUtc.Value, precision);

        // 3) Duration rule (optional)
        if (enforceMinDurationOneDay && toUtc.HasValue)
        {
            if (toUtc.Value < fromUtc.AddDays(1))
            {
                result.InvalidDuration = true;
                result.Errors.Add(Error.BusinessRule(
                    "DateRange.InvalidDuration",
                    $"{dateRangeName}: The assignment duration must be at least one day."));
                return result;
            }
        }

        if (toUtc.HasValue && toUtc.Value < fromUtc)
        {
            // Inverted range => definitely invalid duration
            result.InvalidDuration = true;
            result.Errors.Add(Error.Validation(
                "DateRange.InvalidDuration",
                $"{dateRangeName}: Start date must be before end date."));
            return result;
        }

        // 4) Build overlap predicate
        var param = Expression.Parameter(typeof(T), "x");

        Expression Replace(Expression body, ParameterExpression src, ParameterExpression dest)
            => new ReplaceVisitor(src, dest).Visit(body)!;

        var baseBody = Replace(basePredicate.Body, basePredicate.Parameters[0], param);

        var fromBodyNullable = Replace(fromSelector.Body, fromSelector.Parameters[0], param);
        var toBodyNullable = Replace(toSelector.Body, toSelector.Parameters[0], param);

        var minConst = Expression.Constant(DateTime.MinValue, typeof(DateTime));
        var maxConst = Expression.Constant(DateTime.MaxValue, typeof(DateTime));

        // null => open-ended
        Expression fromBody = Expression.Coalesce(fromBodyNullable, minConst); // DateTime
        Expression toBody = Expression.Coalesce(toBodyNullable, maxConst); // DateTime

        fromBody = ApplyPrecisionExpression(fromBody, precision);
        toBody = ApplyPrecisionExpression(toBody, precision);

        var fromConst = Expression.Constant(fromUtc, typeof(DateTime));
        var toConst = Expression.Constant(toUtc ?? DateTime.MaxValue, typeof(DateTime));

        // Overlap condition
        Expression left, right;
        if (halfOpen)
        {
            // [from, to) style:
            // existing.from < new.to && new.from < existing.to
            left = Expression.LessThan(fromBody, toConst);
            right = Expression.LessThan(fromConst, toBody);
        }
        else
        {
            // [from, to] style:
            // existing.from <= new.to && new.from <= existing.to
            left = Expression.LessThanOrEqual(fromBody, toConst);
            right = Expression.LessThanOrEqual(fromConst, toBody);
        }

        var overlapCond = Expression.AndAlso(left, right);

        var fullOverlapBody = Expression.AndAlso(baseBody, overlapCond);
        var overlapPredicate = Expression.Lambda<Func<T, bool>>(fullOverlapBody, param);

        var overlapCount = await repository.CountAsync(overlapPredicate, cancellationToken);
        if (overlapCount > 0)
        {
            result.HasOverlap = true;
            result.Errors.Add(Error.BusinessRule(
                "DateRange.Overlap",
                $"{dateRangeName}: The date range overlaps with an existing assignment."));
            return result;
        }

        // 5) Continuity rule, if enabled
        if (requireNextFromAfterMaxTo)
        {
            var sameParentPredicate = Expression.Lambda<Func<T, bool>>(baseBody, param);
            var anyForParent = await repository.CountAsync(sameParentPredicate, cancellationToken) > 0;

            if (anyForParent)
            {
                // Continuity by day:
                // We require a record whose To.Date == (From.Date - 1 day)
                // and no record with To > (From.Date - 1 day).
                var prevDay = fromUtc.Date.AddDays(-1);
                var prevDayConst = Expression.Constant(prevDay, typeof(DateTime));

                // x => base && To.Date == prevDay
                var toDate = Expression.Property(toBody, nameof(DateTime.Date));
                var eqPrev = Expression.Equal(toDate, prevDayConst);
                var mustBePrevBody = Expression.AndAlso(baseBody, eqPrev);
                var mustBePrevPredicate = Expression.Lambda<Func<T, bool>>(mustBePrevBody, param);

                var hasExactPrevious = await repository.CountAsync(mustBePrevPredicate, cancellationToken) > 0;

                if (!hasExactPrevious)
                {
                    result.ContinuityBroken = true;
                    result.Errors.Add(Error.BusinessRule(
                        "DateRange.ContinuityBroken",
                        $"{dateRangeName}: The date range does not immediately follow the last assignment."));
                    return result;
                }
                else
                {
                    // x => base && To > prevDay
                    var gtPrev = Expression.GreaterThan(toBody, prevDayConst);
                    var hasLaterBody = Expression.AndAlso(baseBody, gtPrev);
                    var hasLaterPredicate = Expression.Lambda<Func<T, bool>>(hasLaterBody, param);

                    var hasLater = await repository.CountAsync(hasLaterPredicate, cancellationToken) > 0;
                    if (hasLater)
                    {
                        result.ContinuityBroken = true;
                        result.Errors.Add(Error.BusinessRule(
                            "DateRange.ContinuityBroken",
                            $"{dateRangeName}: The date range does not immediately follow the last assignment."));
                        return result;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Truncates DateTime to specified precision using DateTimeUtility methods.
    /// NOTE: Only input values are truncated. Database columns remain at full precision to avoid EF translation issues.
    /// </summary>
    private static DateTime ApplyPrecision(DateTime value, DateTimePrecision precision)
        => precision switch
        {
            DateTimePrecision.Full => value,
            DateTimePrecision.Date => value.Date,
            DateTimePrecision.Hours => value.TruncateToHours(),
            DateTimePrecision.Minutes => value.TruncateToMinutes(),
            DateTimePrecision.Seconds => value.TruncateToSeconds(),
            _ => value
        };

    /// <summary>
    /// Applies precision to database column expressions for SQL generation.
    /// Only Date precision is applied at SQL level. Other precisions leave columns at full precision.
    /// </summary>
    private static Expression ApplyPrecisionExpression(Expression expr, DateTimePrecision precision)
    {
        if (precision == DateTimePrecision.Date)
        {
            // Only Date precision is applied at SQL level (EF Core translates this reliably)
            return Expression.Property(expr, nameof(DateTime.Date));
        }

        // Full, Minutes, Hours, Seconds: leave columns at full precision
        // Input values are truncated instead (see ApplyPrecision method)
        return expr;
    }


    private sealed class ReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _source ? _target : base.VisitParameter(node);
    }
}
