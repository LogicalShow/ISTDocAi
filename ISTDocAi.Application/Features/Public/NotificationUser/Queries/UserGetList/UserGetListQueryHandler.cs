using ISTDocAi.Domain.Common.Shared.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.UserGetList
{
    public class UserGetListQueryHandler(IUserQueryService userQueryService) : IQueryHandler<UserGetListQuery, List<UserGetListQueryResponse>>
    {
        public async Task<Result<List<UserGetListQueryResponse>>> Handle(UserGetListQuery request, CancellationToken cancellationToken)
        {
            var users= await userQueryService.GetAllUsersAsync(cancellationToken);

            var result = users.Select(x=>new UserGetListQueryResponse(x.Id,x.UserName)).ToList();
            return result;
        }
    }
}
