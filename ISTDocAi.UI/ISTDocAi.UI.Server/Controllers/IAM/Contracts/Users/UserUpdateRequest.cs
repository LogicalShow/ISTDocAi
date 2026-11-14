namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserUpdateRequest
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdministrator { get; set; } = false;
    public string? RoleId { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
