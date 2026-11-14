using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.AddRole;

public record AddRoleCommand(
    string NameEn,
    string NameAr,
    bool IsActive,
    int DisplayOrder
) : ICommand<AddRoleCommandResponse>;
