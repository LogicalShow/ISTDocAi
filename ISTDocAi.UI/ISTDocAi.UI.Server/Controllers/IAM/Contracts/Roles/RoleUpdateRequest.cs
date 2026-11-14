namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;

public class RoleUpdateRequest
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
