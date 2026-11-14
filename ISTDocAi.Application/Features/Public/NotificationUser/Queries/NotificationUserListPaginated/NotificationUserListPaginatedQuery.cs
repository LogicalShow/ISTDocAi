using ISTDocAi.Application.Common.Contracts.NotificationService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.SharedModels;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Queries.NotificationUserListPaginated
{
    public record NotificationUserListPaginatedQuery(
        int PageNo = 1,
        int PageRowCount = 10,
        bool UnreadOnly = false
    ) : IQuery<GetListResult<ServerNotificationUserDetail>>;
}
