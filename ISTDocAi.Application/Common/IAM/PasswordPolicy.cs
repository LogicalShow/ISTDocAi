namespace ISTDocAi.Application.Common.IAM;

/// <summary>
/// The Identity password rules the server enforces, exposed so the client can
/// show the same checklist and validate before submitting.
/// </summary>
public sealed record PasswordPolicy(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireNonAlphanumeric,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireDigit);
