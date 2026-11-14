using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Infrastructure.Data.Seeders.IAM;

public static class AuthorizationSeedData
{
    public static readonly ApplicationData[] Applications =
    [
        new ApplicationData { Id = "APP", NameEn = "Application Shell", NameAr = "نظام التطبيق", DisplayOrder = 1, IsActive = true }
    ];

    public static readonly ActionData[] Actions =
    [
        new ActionData { Id = ActionDataEnum.VIEW.ToString(), NameEn = "View", NameAr = "عرض", DisplayOrder = 1 },
        new ActionData { Id = ActionDataEnum.ADD.ToString(), NameEn = "Add", NameAr = "إضافة", DisplayOrder = 2 },
        new ActionData { Id = ActionDataEnum.EDIT.ToString(), NameEn = "Edit", NameAr = "تعديل", DisplayOrder = 3 },
        new ActionData { Id = ActionDataEnum.DELETE.ToString(), NameEn = "Delete", NameAr = "حذف", DisplayOrder = 4 },
        new ActionData { Id = ActionDataEnum.PRINT.ToString(), NameEn = "Print", NameAr = "طباعة", DisplayOrder = 5 }
    ];

    public static readonly ModuleData[] Modules =
    [
        new ModuleData { Id = "ADM", ApplicationId = "APP", ParentId = null, NameEn = "Administration", NameAr = "الإدارة", Icon = "gear-six-light", DisplayOrder = 4, IsActive = true },
        new ModuleData { Id = "ADM.IAM", ApplicationId = "APP", ParentId = "ADM", NameEn = "Security", NameAr = "الأمان", Icon = "tool", DisplayOrder = 5, IsActive = true },
    ];

    public static readonly PageData[] Pages =
    [
        new PageData { Id = "ADM.ATT", ModuleId = "ADM", NameEn = "Attachments", NameAr = "المرفقات", Route = "", ShowInSidebar = false, DisplayOrder = 10, IsActive = true, Actions = [ActionDataEnum.VIEW, ActionDataEnum.ADD, ActionDataEnum.EDIT, ActionDataEnum.DELETE] },
        new PageData { Id = "ADM.IAM.ROL", ModuleId = "ADM.IAM", NameEn = "Roles", NameAr = "الأدوار", Route = "/iam/roles", OpenInNewTab = false, DisplayOrder = 1, IsActive = true, Actions = [ActionDataEnum.VIEW, ActionDataEnum.ADD, ActionDataEnum.EDIT, ActionDataEnum.DELETE, ActionDataEnum.PRINT] },
        new PageData { Id = "ADM.IAM.USR", ModuleId = "ADM.IAM", NameEn = "Users", NameAr = "المستخدمون", Route = "/iam/users", OpenInNewTab = false, DisplayOrder = 2, IsActive = true, Actions = [ActionDataEnum.VIEW, ActionDataEnum.ADD, ActionDataEnum.EDIT, ActionDataEnum.DELETE, ActionDataEnum.PRINT] },
        new PageData { Id = "ADM.SET", ModuleId = "ADM", NameEn = "Settings", NameAr = "الإعدادات", Route = "/public/setting", OpenInNewTab = false, DisplayOrder = 1, IsActive = true, Actions = [ActionDataEnum.VIEW, ActionDataEnum.EDIT] },
        new PageData { Id = "ADM.NOT", ModuleId = "ADM", NameEn = "Notifications", NameAr = "الإشعارات", Route = "/public/notificationSettings", OpenInNewTab = false, DisplayOrder = 2, IsActive = false, Actions = [ActionDataEnum.VIEW, ActionDataEnum.ADD, ActionDataEnum.EDIT, ActionDataEnum.DELETE] }
    ];

    public class ApplicationData
    {
        public string Id { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class ActionData
    {
        public string Id { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    public class ModuleData
    {
        public string Id { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class PageData
    {
        public string Id { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string? ParentPageId { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public bool ShowInSidebar { get; set; } = true;
        public bool OpenInNewTab { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public ActionDataEnum[] Actions { get; set; } = [];
    }
}
