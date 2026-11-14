namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUser;

public class GetUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdministrator { get; set; }
    public string? RoleId { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
