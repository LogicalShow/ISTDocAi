namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUserList;

public class GetUserListResponse
{
    public List<UserListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdministrator { get; set; }
    public string? RoleId { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
