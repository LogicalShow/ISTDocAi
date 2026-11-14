namespace ISTDocAi.Application.Common.Contracts.IAM;

public record RoleModel(
    string Id,
    string Name,
    string NameEn,
    string NameAr,
    bool IsActive,
    int DisplayOrder,
    string ConcurrencyStamp);
