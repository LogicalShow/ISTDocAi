using Microsoft.AspNetCore.Identity;

namespace ISTDocAi.Infrastructure.IAM.Models;

public class IAM_User : IdentityUser
{
    public string FullName { get; set; }

    public bool IsActive { get; set; }

    public bool IsAdministrator { get; set; }

    public string? RoleId { get; set; }
}
