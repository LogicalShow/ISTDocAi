using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.UpdateRole;

public record UpdateRoleCommand(
    string Id,
    string NameEn,
    string NameAr,
    bool IsActive,
    int DisplayOrder,
    string? ConcurrencyStamp
) : ICommand<UpdateRoleCommandResponse>;
