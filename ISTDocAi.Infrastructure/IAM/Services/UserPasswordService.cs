using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;


namespace ISTDocAi.Infrastructure.IAM.Services;
public sealed class UserPasswordService(UserManager<IAM_User> userManager) : IUserPasswordService
{
    public PasswordPolicy GetPasswordPolicy()
    {
        var options = userManager.Options.Password;
        return new PasswordPolicy(
            options.RequiredLength,
            options.RequiredUniqueChars,
            options.RequireNonAlphanumeric,
            options.RequireLowercase,
            options.RequireUppercase,
            options.RequireDigit);
    }

    public async Task<Result> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));
        }
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation(ErrorCodes.PasswordResetFailed, errors));
        }
        return Result.Success();
    }
}
