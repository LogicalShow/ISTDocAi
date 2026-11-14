using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.IAM.Services;

public class UserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserService>? _logger;

    public UserService(IHttpContextAccessor httpContextAccessor, ILogger<UserService>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public string? UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            var user = context.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userId = user.FindFirstValue("uid")
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                _logger?.LogWarning("UserId claim not found. Available claims: {Claims}", 
                    string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));
            }

            return userId;
        }
    }
}
