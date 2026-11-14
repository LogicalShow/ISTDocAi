using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_Module : Entity
{
    public string ApplicationId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public IAM_Application Application { get; set; } = null!;
    public IAM_Module? Parent { get; set; }
    public ICollection<IAM_Module> Children { get; set; } = new List<IAM_Module>();
    public ICollection<IAM_Page> Pages { get; set; } = new List<IAM_Page>();
}
