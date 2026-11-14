namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;

public class RoleAddRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
