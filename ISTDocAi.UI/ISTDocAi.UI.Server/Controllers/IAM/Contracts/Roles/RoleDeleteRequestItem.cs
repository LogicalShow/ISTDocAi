namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;

public class RoleDeleteRequestItem
{
    public string Id { get; set; } = string.Empty;
    public string? ConcurrencyStamp { get; set; }
}
