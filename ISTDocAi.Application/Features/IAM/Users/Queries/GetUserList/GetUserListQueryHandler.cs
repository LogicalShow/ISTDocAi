using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUserList;

public class GetUserListQueryHandler(IUserManagementService userManagementService) : IQueryHandler<GetUserListQuery, GetUserListResponse>
{
    public async Task<Result<GetUserListResponse>> Handle(GetUserListQuery request, CancellationToken cancellationToken)
    {
        var result = await userManagementService.GetListAsync(
            request.UserName,
            request.Email,
            request.SortField,
            request.SortOrder,
            request.PageNo,
            request.PageRowCount,
            cancellationToken);

        var response = new GetUserListResponse
        {
            TotalCount = result.TotalCount,
            Items = result.Items.Select(u => new UserListItem
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                IsActive = u.IsActive,
                IsAdministrator = u.IsAdministrator,
                RoleId = u.RoleId,
                ConcurrencyStamp = u.ConcurrencyStamp
            }).ToList()
        };

        return Result<GetUserListResponse>.Success(response);
    }
}
