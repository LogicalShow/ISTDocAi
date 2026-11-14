using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_Application : Entity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<IAM_Module> Modules { get; set; } = new List<IAM_Module>();
}
