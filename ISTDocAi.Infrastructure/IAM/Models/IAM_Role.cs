using Microsoft.AspNetCore.Identity;

namespace ISTDocAi.Infrastructure.IAM.Models;

public class IAM_Role : IdentityRole
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

