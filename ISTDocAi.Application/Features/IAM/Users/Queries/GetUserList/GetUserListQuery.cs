using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUserList;

public record GetUserListQuery(
    string? UserName,
    string? Email,
    string? SortField,
    string? SortOrder,
    int PageNo,
    int PageRowCount
) : IQuery<GetUserListResponse>;
