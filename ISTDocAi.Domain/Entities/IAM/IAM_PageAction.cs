using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_PageAction : Entity
{
    public string PageId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public IAM_Page Page { get; set; } = null!;
    public IAM_Action Action { get; set; } = null!;
}
