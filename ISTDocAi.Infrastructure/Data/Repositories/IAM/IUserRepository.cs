using ISTDocAi.Application.Common.Contracts.UserQuery;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Repositories.IAM;

public interface IUserRepository
{
    Task<List<IAM_User>> GetAllUsersAsync(CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetUserEmailsByNotificationId(string notificationId, CancellationToken ct);

    Task<List<IAM_User>> SearchUsersAsync(string? term, int pageNo, int pageRowCount, CancellationToken cancellationToken);

    Task<string?> GetUserNameById(string userId, CancellationToken cancellationToken);

    Task<Dictionary<string, string>> GetFullNamesByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);
    Task<UserLookupModel?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<string?> GetUserEmailByIdAsync(string userId,CancellationToken cancellationToken);
}
