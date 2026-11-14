namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserAddRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsAdministrator { get; set; } = false;
    public string? RoleId { get; set; }
}
