using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Infrastructure.IAM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.IAM.Services;

public class RoleService(RoleManager<IAM_Role> roleManager) : IRoleService
{
    public async Task<RoleModel?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);
        return role is null ? null : ToModel(role);
    }

    public async Task<RoleListResult> GetListAsync(
        string? name,
        string? sortField,
        string? sortOrder,
        int pageNo,
        int pageRowCount,
        CancellationToken cancellationToken)
    {
        var query = roleManager.Roles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(r =>
                (r.NameEn != null && r.NameEn.Contains(name)) ||
                (r.NameAr != null && r.NameAr.Contains(name)) ||
                (r.Name != null && r.Name.Contains(name)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortField?.ToLowerInvariant() switch
        {
            "nameen" => sortOrder == "desc" ? query.OrderByDescending(r => r.NameEn) : query.OrderBy(r => r.NameEn),
            "name" => sortOrder == "desc" ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            _ => query.OrderBy(r => r.DisplayOrder).ThenBy(r => r.NameEn)
        };

        var effectivePageNo = pageNo > 0 ? pageNo : 1;
        var effectivePageSize = pageRowCount > 0 ? pageRowCount : 10;

        var roles = await query
            .Skip((effectivePageNo - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new RoleListResult(roles.Select(ToModel).ToList(), totalCount);
    }

    public async Task<Result<string>> AddAsync(
        string nameEn,
        string nameAr,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var role = new IAM_Role
        {
            Name = nameEn.Trim(),
            NameEn = nameEn.Trim(),
            NameAr = nameAr ?? string.Empty,
            IsActive = isActive,
            DisplayOrder = displayOrder
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => Error.Validation(ErrorCodes.RoleIdentity(e.Code), e.Description)).ToArray();
            return Result<string>.Failure(errors);
        }

        return Result<string>.Success(role.Id);
    }

    public async Task<Result> UpdateAsync(
        string id,
        string nameEn,
        string nameAr,
        bool isActive,
        int displayOrder,
        string? concurrencyStamp,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null)
            return Result.Failure(Error.NotFound(ErrorCodes.RoleNotFound, "Role not found"));

        if (!string.IsNullOrWhiteSpace(concurrencyStamp) && role.ConcurrencyStamp != concurrencyStamp)
            return Result.Failure(Error.Conflict(ErrorCodes.RoleConflict, "Role has been modified by another user"));

        role.Name = nameEn.Trim();
        role.NameEn = nameEn.Trim();
        role.NameAr = nameAr ?? string.Empty;
        role.IsActive = isActive;
        role.DisplayOrder = displayOrder;

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => Error.Validation(ErrorCodes.RoleIdentity(e.Code), e.Description)).ToArray();
            return Result.Failure(errors);
        }

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(IReadOnlyCollection<RoleDeleteItem> items, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();
        foreach (var item in items)
        {
            var role = await roleManager.FindByIdAsync(item.Id);
            if (role is null)
            {
                errors.Add(Error.NotFound(ErrorCodes.RoleNotFound, $"Role with ID {item.Id} not found"));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.ConcurrencyStamp) && role.ConcurrencyStamp != item.ConcurrencyStamp)
            {
                errors.Add(Error.Conflict(ErrorCodes.RoleConflict, $"Role {role.Name} has been modified by another user"));
                continue;
            }

            var result = await roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(e => Error.Validation(ErrorCodes.RoleIdentity(e.Code), $"{role.Name}: {e.Description}")));
            }
        }

        return errors.Count > 0 ? Result.Failure(errors.ToArray()) : Result.Success();
    }

    private static RoleModel ToModel(IAM_Role role) => new(
        role.Id,
        role.Name ?? string.Empty,
        role.NameEn,
        role.NameAr,
        role.IsActive,
        role.DisplayOrder,
        role.ConcurrencyStamp ?? string.Empty);
}
