using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_Action : Entity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<IAM_PageAction> PageActions { get; set; } = new List<IAM_PageAction>();
}
