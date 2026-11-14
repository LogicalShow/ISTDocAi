namespace ISTDocAi.Application.Features.IAM.Auth.Queries.GetPasswordPolicy;

public sealed record GetPasswordPolicyResponse(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireNonAlphanumeric,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireDigit);
