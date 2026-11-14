using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_Page : Entity
{
    public string ModuleId { get; set; } = string.Empty;
    public string? ParentPageId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Route { get; set; }
    public bool ShowInSidebar { get; set; } = true;
    public bool OpenInNewTab { get; set; } = false;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public IAM_Module Module { get; set; } = null!;
    public IAM_Page? ParentPage { get; set; }
    public ICollection<IAM_Page> ChildPages { get; set; } = new List<IAM_Page>();
    public ICollection<IAM_PageAction> PageActions { get; set; } = new List<IAM_PageAction>();
}
