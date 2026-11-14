using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Domain.Dtos.Shared.DateRangeValidation;

public sealed class DateRangeValidationResult
{
    /// <summary>
    /// True if the range violates duration rule (e.g. shorter than allowed).
    /// </summary>
    public bool InvalidDuration { get; set; }

    /// <summary>
    /// True if the range overlaps with existing ranges for the same base predicate.
    /// </summary>
    public bool HasOverlap { get; set; }

    /// <summary>
    /// True if the continuity rule is broken (when enabled).
    /// </summary>
    public bool ContinuityBroken { get; set; }

    /// <summary>
    /// True if any rule failed.
    /// </summary>
    public bool HasAnyConflict =>
        InvalidDuration || HasOverlap || ContinuityBroken;

    /// <summary>
    /// True if validation passed (no conflicts).
    /// </summary>
    public bool IsValid => !HasAnyConflict;

    /// <summary>
    /// List of errors encountered during validation.
    /// </summary>
    public List<Error> Errors { get; set; } = new();

    public static DateRangeValidationResult None => new();
}

