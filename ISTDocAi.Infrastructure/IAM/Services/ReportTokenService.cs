using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ISTDocAi.Infrastructure.IAM.Services;

public static class JwtAudience
{
    public const string Default = "JwtAudience";

    // Separate audience so a report token is rejected by the default API scheme.
    public const string Report = "JwtAudience.Report";
}

public static class ReportAuthenticationDefaults
{
    public const string AuthenticationScheme = "ReportBearer";

    // Form field that carries the report token in the POST that opens a report tab.
    public const string FormField = "reportToken";

    public const string PurposeClaim = "purpose";
    public const string PurposeValue = "report";

    public const string PathPrefix = "/mvc/Report";
}

public sealed record IssuedReportToken(string Token, int ExpiresInSeconds);

public interface IReportTokenService
{
    IssuedReportToken Generate(string userId);
}

/// <summary>
/// Issues short-lived, identity-only tokens that authenticate report tabs (/mvc/Report/...)
/// through the <see cref="ReportAuthenticationDefaults.AuthenticationScheme"/> scheme.
/// Permissions still come from the server-side permission cache.
/// </summary>
public sealed class ReportTokenService(
    TimeProvider timeProvider,
    IOptionsMonitor<ReportTokenOptions> options) : IReportTokenService
{
    public IssuedReportToken Generate(string userId)
    {
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("JWT_SECRET_KEY environment variable is required and cannot be empty.");

        // CurrentValue follows appsettings.json edits at runtime; clamp so a bad value
        // can neither break report opening nor issue long-lived tokens.
        var lifetimeSeconds = Math.Clamp(
            options.CurrentValue.LifetimeSeconds,
            ReportTokenOptions.MinLifetimeSeconds,
            ReportTokenOptions.MaxLifetimeSeconds);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("uid", userId),
            new Claim(ReportAuthenticationDefaults.PurposeClaim, ReportAuthenticationDefaults.PurposeValue)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "JwtIssuer",
            audience: JwtAudience.Report,
            claims: claims,
            expires: timeProvider.GetUtcNow().UtcDateTime.AddSeconds(lifetimeSeconds),
            signingCredentials: creds);

        return new IssuedReportToken(new JwtSecurityTokenHandler().WriteToken(token), lifetimeSeconds);
    }
}
