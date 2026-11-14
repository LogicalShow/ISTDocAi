using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    string Id,
    string UserName,
    string? Email,
    string? FullName,
    bool IsActive,
    bool IsAdministrator,
    string? RoleId,
    string? ConcurrencyStamp
) : ICommand<UpdateUserCommandResponse>;
