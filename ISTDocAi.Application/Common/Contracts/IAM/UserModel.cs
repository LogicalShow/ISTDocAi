namespace ISTDocAi.Application.Common.Contracts.IAM;

public record UserModel(
    string Id,
    string UserName,
    string? Email,
    string FullName,
    bool IsActive,
    bool IsAdministrator,
    string? RoleId,
    string ConcurrencyStamp);
