namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;

public class RoleGetListRequest
{
    public string? Name { get; set; }
    public string? SortField { get; set; }
    public string? SortOrder { get; set; }
    public int PageNo { get; set; } = 1;
    public int PageRowCount { get; set; } = 10;
}
