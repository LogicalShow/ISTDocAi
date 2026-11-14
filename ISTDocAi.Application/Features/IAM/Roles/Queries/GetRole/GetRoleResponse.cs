namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRole;
public class GetRoleResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}