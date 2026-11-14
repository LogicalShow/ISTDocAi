using ISTDocAi.Application.Common.Interfaces.IAM;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.IAM.Services;

public interface IAuthTokenService
{
    Task<string> GenerateTokenAsync(IAM_User user);
}

public class AuthTokenService : IAuthTokenService
{
    private readonly UserManager<IAM_User> _userManager;
    private readonly TimeProvider _timeProvider;

    public AuthTokenService(
        UserManager<IAM_User> userManager,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    public async Task<string> GenerateTokenAsync(IAM_User user)
    {
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        var userClaims = await _userManager.GetClaimsAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("uid", user.Id)
        };

        // Add other non-permission claims
        claims.AddRange(userClaims.Where(c => c.Type is not "permission" and not "p"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "JwtIssuer",
            audience: JwtAudience.Default,
            claims: claims,
            expires: _timeProvider.GetUtcNow().UtcDateTime.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
