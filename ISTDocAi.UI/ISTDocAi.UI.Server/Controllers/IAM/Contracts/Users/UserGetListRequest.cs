namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserGetListRequest
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? SortField { get; set; }
    public string? SortOrder { get; set; }
    public int PageNo { get; set; } = 1;
    public int PageRowCount { get; set; } = 10;
}
