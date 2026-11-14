namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserChangePasswordRequest
{
    public string Id { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string? ConcurrencyStamp { get; set; }
}
