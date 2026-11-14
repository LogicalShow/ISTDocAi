using ISTDocAi.Application.Common.Contracts.UserQuery;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Infrastructure.Data.Repositories.IAM;

namespace ISTDocAi.Infrastructure.IAM.Services
{
    public class UserQueryService(IUserRepository repository) : IUserQueryService
    {
        public async Task<List<UserLookupModel>> GetAllUsersAsync(CancellationToken cancellationToken)
        {
           var users = await repository.GetAllUsersAsync(cancellationToken);

             return users.Select(u => new UserLookupModel
             ( u.Id,
               u.UserName
              )).ToList();
        }

        public async Task<IEnumerable<string>> GetUserEmailsByNotificationId(string notificationId, CancellationToken ct)
        {
         var result= await repository.GetUserEmailsByNotificationId(notificationId, ct);
            return result;
        }

        public async Task<string?> GetUserNameById(string userId, CancellationToken cancellationToken)
        {
          var result = await repository.GetUserNameById(userId, cancellationToken);

            return result;
        }

        public async Task<Dictionary<string, string>> GetFullNamesByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return await repository.GetFullNamesByIdsAsync(userIds, cancellationToken);
        }
        public async Task<UserLookupModel?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return await repository.GetUserByEmailAsync(email, cancellationToken);
        }
        public async Task<string?> GetUserEmailByIdAsync(string userId,CancellationToken cancellationToken)
        {
            return await repository.GetUserEmailByIdAsync(userId,cancellationToken);
        }
    }
}

