using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IRoleService
{
    Task<RoleModel?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<RoleListResult> GetListAsync(
        string? name,
        string? sortField,
        string? sortOrder,
        int pageNo,
        int pageRowCount,
        CancellationToken cancellationToken);

    Task<Result<string>> AddAsync(
        string nameEn,
        string nameAr,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Result> UpdateAsync(
        string id,
        string nameEn,
        string nameAr,
        bool isActive,
        int displayOrder,
        string? concurrencyStamp,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        IReadOnlyCollection<RoleDeleteItem> items,
        CancellationToken cancellationToken);
}
