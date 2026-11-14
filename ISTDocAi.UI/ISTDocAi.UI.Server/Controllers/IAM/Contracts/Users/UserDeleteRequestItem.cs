namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserDeleteRequestItem
{
    public string Id { get; set; } = string.Empty;
    public string? ConcurrencyStamp { get; set; }
}
