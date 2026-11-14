using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Application.Features.IAM.Auth.Models;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Identity;
using ISTDocAi.Infrastructure.IAM.Models;
using ISTDocAi.Infrastructure.Realtime;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Implementation of authentication service using ASP.NET Core Identity.
/// </summary>
public class UserAuthenticationService(
    UserManager<IAM_User> userManager,
    IAuthTokenService authTokenService,
    IRealtimeService realtimeService,
    TimeProvider timeProvider) : IUserAuthenticationService
{
    public async Task<Result<AuthenticationResult>> AuthenticateAsync(
        string username,
        string password,
        string? connectionId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user == null || !await userManager.CheckPasswordAsync(user, password))
        {
            return Result.Failure<AuthenticationResult>(
                Error.Unauthorized(ErrorCodes.AuthInvalidCredentials, "Invalid username or password"));
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthenticationResult>(
                Error.Unauthorized(ErrorCodes.AuthInactiveUser, "User account is inactive"));
        }

        string authToken = await authTokenService.GenerateTokenAsync(user);

        // Notify all other connected users about the login
        var loginNotification = new ServerNotification
        {
            Id = Guid.NewGuid().ToString(),
            Type = "Online",
            Message = $"{user.FullName} has logged in.",
            Time = timeProvider.GetLocalDateTime()
        };
        await realtimeService.NotifyExceptAsync(loginNotification, connectionId, cancellationToken);

        return Result.Success(new AuthenticationResult(
            AuthToken: authToken,
            FullName: user.FullName,
            Email: user.Email ?? string.Empty));
    }
}
