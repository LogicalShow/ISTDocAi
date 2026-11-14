using ISTDocAi.Application.Features.IAM.Auth.Commands.ChangeCurrentUserPassword;
using ISTDocAi.Application.Features.IAM.Auth.Commands.Login;
using ISTDocAi.Application.Features.IAM.Auth.Commands.ResetPassword;
using ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpEmail;
using ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpToCurrentUser;
using ISTDocAi.Application.Features.IAM.Auth.Commands.VerifyOtp;
using ISTDocAi.Application.Features.IAM.Auth.Queries.GetPasswordPolicy;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Controllers.IAM.Contracts.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ISTDocAi.UI.Server.RateLimiting;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Infrastructure.IAM.Services;

namespace ISTDocAi.UI.Server.Controllers.IAM;

[Route("api/iam/[controller]/[action]")]
public class AuthController(
    IMediator mediator,
    IPermissionCacheService permissionCache,
    IReportTokenService reportTokenService) : BaseController
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> PasswordPolicy(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPasswordPolicyQuery(), cancellationToken);
        return Respond(result);
    }

    [AllowAnonymous]
    // Anonymous credential endpoint - metered per client address to slow brute force.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        string? connectionId = Request.Headers["X-SignalR-ConnectionId"];
        var command = new LoginCommand(request.Username, request.Password, connectionId);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Respond(result);
        }

        return Respond(Result.Success(new
        {
            authToken = result.Value.AuthToken,
            fullName = result.Value.FullName,
            email = result.Value.Email
        }));
    }
    [AllowAnonymous]
    // Anonymous credential endpoint - metered per client address to slow brute force.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost]
    public async Task<IActionResult> SendOtpEmail([FromBody] SendOtpEmailRequest request, CancellationToken cancellationToken)
    {
        var command = new SendOtpEmailCommand(request.Email);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    [AllowAnonymous]
    // Anonymous credential endpoint - metered per client address to slow brute force.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var command = new VerifyOtpCommand(request.Email, request.Otp);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    [AllowAnonymous]
    // Anonymous credential endpoint - metered per client address to slow brute force.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Email, request.NewPassword, request.Otp);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    [AllowAnonymous]
    // Anonymous credential endpoint - metered per client address to slow brute force.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost]
    public IActionResult Logout()
    {
        return Respond();
    }

    [HttpPost]
    public async Task<IActionResult> SendOtpToCurrentUser(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SendOtpToCurrentUserCommand(), cancellationToken);
        return Respond(result);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeCurrentUserPassword([FromBody] ChangeCurrentUserPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeCurrentUserPasswordCommand(request.Otp, request.NewPassword);
        var result = await mediator.Send(command, cancellationToken);
        return Respond(result);
    }

    // Current user's permissions, served from the server-side permission cache
    // (they are no longer carried in the JWT).
    [HttpGet]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Respond(Result.Failure(Error.Unauthorized(ErrorCodes.AuthUserIdentityMissing, "User identity is missing from the token")));
        }

        var snapshot = await permissionCache.GetAsync(userId, cancellationToken);
        if (!snapshot.IsAllowed)
        {
            return Respond(Result.Failure(Error.Unauthorized(ErrorCodes.AuthInactiveUser, "User is inactive or no longer exists")));
        }

        return Respond(Result.Success(new
        {
            permissions = snapshot.Compressed,
            version = snapshot.Version
        }));
    }

    // Short-lived token for opening server-rendered reports (/mvc/Report/...) in a new tab,
    // which cannot send the Authorization header. Lifetime comes from "ReportToken" in appsettings.json.
    [HttpPost]
    public IActionResult ReportToken()
    {
        var userId = GetUserIdFromToken();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Respond(Result.Failure(Error.Unauthorized(ErrorCodes.AuthUserIdentityMissing, "User identity is missing from the token")));
        }

        var issued = reportTokenService.Generate(userId);
        return Respond(Result.Success(new
        {
            reportToken = issued.Token,
            expiresInSeconds = issued.ExpiresInSeconds
        }));
    }
}
