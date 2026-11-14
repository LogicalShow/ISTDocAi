using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Contracts.UserQuery;

namespace ISTDocAi.Application.Common.Interfaces.IAM
{
    public interface IUserQueryService
    {
        Task<List<UserLookupModel>> GetAllUsersAsync(CancellationToken cancellationToken);

        Task<IEnumerable<string>> GetUserEmailsByNotificationId(string notificationId, CancellationToken ct);
        Task<string?>  GetUserNameById(string userId, CancellationToken cancellationToken);

        Task<Dictionary<string, string>> GetFullNamesByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);
        Task<UserLookupModel?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
        Task<string?> GetUserEmailByIdAsync(string userId,CancellationToken cancellationToken);
    }
}

