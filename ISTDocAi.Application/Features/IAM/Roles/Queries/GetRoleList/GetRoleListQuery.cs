using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRoleList;

public record GetRoleListQuery(
    string? Name,
    string? SortField,
    string? SortOrder,
    int PageNo,
    int PageRowCount
) : IQuery<GetRoleListResponse>;
