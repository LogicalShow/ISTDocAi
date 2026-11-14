using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IUserManagementService
{
    Task<UserModel?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<UserListResult> GetListAsync(
        string? userName,
        string? email,
        string? sortField,
        string? sortOrder,
        int pageNo,
        int pageRowCount,
        CancellationToken cancellationToken);

    Task<Result<string>> AddAsync(
        string userName,
        string email,
        string? fullName,
        string password,
        bool isActive,
        bool isAdministrator,
        string? roleId,
        CancellationToken cancellationToken);

    Task<Result> UpdateAsync(
        string id,
        string userName,
        string? email,
        string? fullName,
        bool isActive,
        bool isAdministrator,
        string? roleId,
        string? concurrencyStamp,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        IReadOnlyCollection<UserDeleteItem> items,
        CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(
        string id,
        string newPassword,
        string? concurrencyStamp,
        CancellationToken cancellationToken);
}
