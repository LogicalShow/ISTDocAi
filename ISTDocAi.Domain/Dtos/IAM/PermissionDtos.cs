namespace ISTDocAi.Domain.Dtos.IAM;

public class RolePermissionsDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleNameEn { get; set; } = string.Empty;
    public string RoleNameAr { get; set; } = string.Empty;
    public List<ApplicationModuleDto> Applications { get; set; } = new();
}

public class UserPermissionsDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<ApplicationModuleDto> Applications { get; set; } = new();
}

public class ApplicationModuleDto
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public List<ModuleDto> Modules { get; set; } = new();
}

public class ModuleDto
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public PageDto? DirectPage { get; set; }
    public List<PageDto> Pages { get; set; } = new();
    public List<ModuleDto> Children { get; set; } = new();
}

public class PageDto
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public List<PageActionDto> Actions { get; set; } = new();
    public List<PageDto> ChildPages { get; set; } = new();
}

public class PageActionDto
{
    public string Id { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ActionNameEn { get; set; } = string.Empty;
    public string ActionNameAr { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}
