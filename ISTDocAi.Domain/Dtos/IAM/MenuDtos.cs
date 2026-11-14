namespace ISTDocAi.Domain.Dtos.IAM;

public class UserMenuDto
{
    public List<MenuModuleDto> Modules { get; set; } = new();
}

public class MenuModuleDto
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? Route { get; set; }
    public bool OpenInNewTab { get; set; }
    public List<MenuModuleDto> SubModules { get; set; } = new();
    public List<MenuPageDto> Pages { get; set; } = new();
}

public class MenuPageDto
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public bool OpenInNewTab { get; set; }
    public int DisplayOrder { get; set; }
}
