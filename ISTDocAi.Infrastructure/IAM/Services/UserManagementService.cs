using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Application.Features.IAM.Events;
using MediatR;

namespace ISTDocAi.Infrastructure.IAM.Services;

public class UserManagementService(UserManager<IAM_User> userManager, IUserPasswordService userPasswordService, IPublisher publisher) : IUserManagementService
{
    public async Task<UserModel?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        return user is null ? null : ToModel(user);
    }

    public async Task<UserListResult> GetListAsync(
        string? userName,
        string? email,
        string? sortField,
        string? sortOrder,
        int pageNo,
        int pageRowCount,
        CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(u => u.UserName != null && u.UserName.Contains(userName));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email != null && u.Email.Contains(email));

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortField?.ToLowerInvariant() switch
        {
            "email" => sortOrder == "desc" ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "fullname" => sortOrder == "desc" ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
            _ => sortOrder == "desc" ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName)
        };

        var effectivePageNo = pageNo > 0 ? pageNo : 1;
        var effectivePageSize = pageRowCount > 0 ? pageRowCount : 10;

        var users = await query
            .Skip((effectivePageNo - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new UserListResult(users.Select(ToModel).ToList(), totalCount);
    }

    public async Task<Result<string>> AddAsync(
        string userName,
        string email,
        string? fullName,
        string password,
        bool isActive,
        bool isAdministrator,
        string? roleId,
        CancellationToken cancellationToken)
    {
        var existingUserByEmail = await userManager.FindByEmailAsync(email.Trim());
        if (existingUserByEmail != null)
            return Result<string>.Failure(Error.Conflict(ErrorCodes.UserDuplicateEmail, "A user with this email already exists"));

        var user = new IAM_User
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            FullName = fullName?.Trim() ?? string.Empty,
            IsActive = isActive,
            IsAdministrator = isAdministrator,
            RoleId = string.IsNullOrWhiteSpace(roleId) ? null : roleId.Trim()
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => Error.Validation(ErrorCodes.UserIdentity(e.Code), e.Description)).ToArray();
            return Result<string>.Failure(errors);
        }

        return Result<string>.Success(user.Id);
    }

    public async Task<Result> UpdateAsync(
        string id,
        string userName,
        string? email,
        string? fullName,
        bool isActive,
        bool isAdministrator,
        string? roleId,
        string? concurrencyStamp,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        if (!string.IsNullOrWhiteSpace(concurrencyStamp) && user.ConcurrencyStamp != concurrencyStamp)
            return Result.Failure(Error.Conflict(ErrorCodes.UserConflict, "User has been modified by another user"));

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUserByEmail = await userManager.FindByEmailAsync(email.Trim());
            if (existingUserByEmail != null && existingUserByEmail.Id != id)
                return Result.Failure(Error.Conflict(ErrorCodes.UserDuplicateEmail, "A user with this email already exists"));
        }

        var activeChanged = user.IsActive != isActive;
        user.UserName = userName.Trim();
        user.Email = email?.Trim();
        user.FullName = fullName?.Trim() ?? string.Empty;
        user.IsActive = isActive;
        user.IsAdministrator = isAdministrator;
        user.RoleId = string.IsNullOrWhiteSpace(roleId) ? null : roleId.Trim();

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result.Failure(result.Errors.Select(e => Error.Validation(ErrorCodes.UserIdentity(e.Code), e.Description)).ToArray());

        // Deactivation must take effect on live sessions: invalidate and notify the user's client.
        if (activeChanged)
            await publisher.Publish(new UserPermissionsChangedNotification([id], PermissionChangeReason.UserActiveStateChanged), CancellationToken.None);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(IReadOnlyCollection<UserDeleteItem> items, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();
        var deletedUserIds = new List<string>();
        foreach (var item in items)
        {
            var user = await userManager.FindByIdAsync(item.Id);
            if (user is null)
            {
                errors.Add(Error.NotFound(ErrorCodes.UserNotFound, $"User with ID {item.Id} not found"));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.ConcurrencyStamp) && user.ConcurrencyStamp != item.ConcurrencyStamp)
            {
                errors.Add(Error.Conflict(ErrorCodes.UserConflict, $"User {user.UserName} has been modified by another user"));
                continue;
            }

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(e => Error.Validation(ErrorCodes.UserIdentity(e.Code), $"{user.UserName}: {e.Description}")));
            }
            else
            {
                deletedUserIds.Add(item.Id);
            }
        }

        // Not cancellable: the deletions are committed, so cached permissions must be dropped.
        if (deletedUserIds.Count > 0)
            await publisher.Publish(new UserPermissionsChangedNotification(deletedUserIds, PermissionChangeReason.UserDeleted), CancellationToken.None);

        return errors.Count > 0 ? Result.Failure(errors.ToArray()) : Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(string id, string newPassword, string? concurrencyStamp, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        if (!string.IsNullOrWhiteSpace(concurrencyStamp) && user.ConcurrencyStamp != concurrencyStamp)
            return Result.Failure(Error.Conflict(ErrorCodes.UserConflict, "User has been modified by another user"));

        return await userPasswordService.ResetPasswordAsync(id, newPassword, cancellationToken);
    }

    private static UserModel ToModel(IAM_User user) => new(
        user.Id,
        user.UserName ?? string.Empty,
        user.Email,
        user.FullName ?? string.Empty,
        user.IsActive,
        user.IsAdministrator,
        user.RoleId,
        user.ConcurrencyStamp ?? string.Empty);
}
