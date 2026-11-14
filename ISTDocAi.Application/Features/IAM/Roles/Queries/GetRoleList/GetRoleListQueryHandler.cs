using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRoleList;

public class GetRoleListQueryHandler(IRoleService roleService) : IQueryHandler<GetRoleListQuery, GetRoleListResponse>
{
    public async Task<Result<GetRoleListResponse>> Handle(GetRoleListQuery request, CancellationToken cancellationToken)
    {
        var result = await roleService.GetListAsync(
            request.Name,
            request.SortField,
            request.SortOrder,
            request.PageNo,
            request.PageRowCount,
            cancellationToken);

        var response = new GetRoleListResponse
        {
            TotalCount = result.TotalCount,
            Items = result.Items.Select(r => new RoleListItem
            {
                Id = r.Id,
                Name = r.Name,
                NameEn = r.NameEn,
                NameAr = r.NameAr,
                IsActive = r.IsActive,
                DisplayOrder = r.DisplayOrder,
                ConcurrencyStamp = r.ConcurrencyStamp
            }).ToList()
        };

        return Result<GetRoleListResponse>.Success(response);
    }
}
