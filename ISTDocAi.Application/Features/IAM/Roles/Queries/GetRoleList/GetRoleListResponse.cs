namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRoleList;

public class GetRoleListResponse
{
    public List<RoleListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class RoleListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
